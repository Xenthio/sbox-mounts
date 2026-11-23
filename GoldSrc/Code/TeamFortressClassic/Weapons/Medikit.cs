using System;
using GoldSrc.HalfLife;
using GoldSrc.Maps;

namespace GoldSrc.TeamFortressClassic;

[Title( "Medikit" ), Category( "Team Fortress Classic" )]
[WeaponPickup( "tfc_medkit", null, 0, EntityName = "tf_weapon_medikit", PickupModel = "p_medkit", Bucket = 0, Weight = 3, Mount = "tfc" )]
public sealed class Medikit : CrowbarWeapon
{
	TimeUntil _nextTreatment;
	internal override string WorldModelPath => "models/p_medkit.mdl";
	internal override (string Name, float? Delay) IdleAnimation => (Game.Random.Int( 0, 1 ) == 0 ? "idle" : "longidle", 7.5f);

	internal override void Configure()
	{
		UsesAmmo = UsesClips = false;
		HoldType = WeaponHolds.Melee;
		PrimaryDelay = SecondaryDelay = 0.4f;
		Ballistics = new() { Range = 32, Radius = 0 };
	}

	// Sandbox has no TFC team assignment. The two triggers explicitly select treatment or infection.
	public override void PrimaryAttack() => Use( false );
	public override void SecondaryAttack() => Use( true );

	void Use( bool infect )
	{
		SetNextFire( PrimaryDelay );
		var hit = TraceSwing( AimRay );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( hit.Hit ? "longuse" : "shortuse", idleAfter: 5 );
		ShootEffects( new ShotEffect( hit.EndPosition, hit.Hit, hit.Normal, hit.GameObject, hit.Surface ) );
		ApplyTreatment( infect );
	}

	protected override void OnShootEffects( ShotEffect shot ) => PostWeaponEvent( WeaponEvents.Attack );

	[Rpc.Host( NetFlags.OwnerOnly )]
	void ApplyTreatment( bool infect )
	{
		if ( !IsActive || !Owner.IsValid() || _nextTreatment > 0 ) return;
		_nextTreatment = PrimaryDelay;
		var aim = AimRay;
		var hit = TraceSwing( aim );
		var player = hit.GameObject?.GetComponentInParent<PlayerController>();
		if ( !player.IsValid() || !player.Enabled || MapState.PlayerVitals( player ) is not { Health: > 0 } vitals ) return;

		if ( infect )
		{
			var damage = new DamageInfo { Damage = 10, Attacker = Owner.GameObject, Weapon = GameObject,
				Origin = aim.Position, Position = hit.EndPosition, Hitbox = hit.Hitbox };
			damage.Tags.Add( "club" );
			vitals.OnDamage( damage );
			if ( vitals.Health > 0 ) MedikitInfection.Infect( player, Owner.GameObject );
			return;
		}

		var cured = Cure<MedikitInfection>( player ) | Cure<BurningPlayer>( player ) | Cure<Tranquilized>( player ) |
			Cure<GasHallucination>( player ) | Cure<LegWound>( player );
		if ( cured ) Emit( "ambience/steamburst1.wav" );
		using ( Rpc.FilterInclude( player.Network.Owner ?? Connection.Local ) )
			CureConcussion( player );

		if ( vitals.Health < vitals.MaxHealth )
		{
			vitals.Health = MathF.Min( vitals.Health + 200, vitals.MaxHealth );
			Emit( "items/medshot4.wav" );
			return;
		}

		if ( vitals.Health >= vitals.MaxHealth + 50 ) return;
		vitals.Health = MathF.Min( vitals.Health + 5, vitals.MaxHealth + 50 );
		MedikitOverheal.StartDecay( player );
		Emit( "items/medshot5.wav" );
	}

	static bool Cure<T>( PlayerController player ) where T : Component
	{
		var status = player.GetComponentInChildren<T>();
		if ( !status.IsValid() ) return false;
		status.GameObject.Destroy();
		return true;
	}

	void Emit( string sample ) => ContentMount?.NpcAudio.Emit( sample, WorldPosition, attenuation: 0.8f );

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void CureConcussion( PlayerController player )
	{
		if ( player.IsValid() && !player.IsProxy ) ConcussionView.Cure( player );
	}
}

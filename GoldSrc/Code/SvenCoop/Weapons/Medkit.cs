using System;
using GoldSrc.HalfLife;
using GoldSrc.Maps;
using Sandbox.Npcs;

namespace GoldSrc.SvenCoop;

[Title( "Medkit" ), Category( "Sven Co-op" )]
[WeaponPickup( "medkit", "health", 100, PickupModel = "w_pmedkit", Bucket = 0, Weight = -5, Mount = "svencoop" )]
public sealed partial class Medkit : CrowbarWeapon
{
	TimeUntil _nextTreatment;
	internal override string WorldModelPath => "models/p_medkit.mdl";
	internal override bool SwitchWhenSpent => false;
	internal override (string Name, float? Delay) IdleAnimation => (Game.Random.Float( 0, 1 ) < 0.9f ? "idle" : "longidle", 2);

	internal override void Configure()
	{
		UsesClips = false;
		StartingAmmo = 50;
		HoldType = WeaponHolds.Throwable;
		PrimaryAutomatic = SecondaryAutomatic = true;
		SecondaryDelay = 0.1f;
		PrimaryDelay = DeployTime = 0.5f;
		Ballistics = new() { Range = 32 };
	}

	protected override void OnAdded( BaseInventoryComponent inventory )
	{
		base.OnAdded( inventory );
		if ( Networking.IsHost ) inventory.GameObject.GetOrAddComponent<MedkitRecharge>().Bind( this );
	}

	public override void PrimaryAttack()
	{
		SetNextFire( PrimaryDelay );
		Treat();
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void Treat()
	{
		if ( !IsActive || !Owner.IsValid() || !Inventory.IsValid() || PrimaryAmmoType is null || _nextTreatment > 0 || _reviveTarget.IsValid() ||
			MapState.PlayerVitals( Owner ) is not { Health: > 0 } healer ) return;

		var charge = Inventory.GetAmmo( PrimaryAmmoType );
		if ( charge <= 0 ) return;
		var hit = TraceSwing( AimRay );
		if ( !hit.GameObject.IsValid() || FindPatient( hit.GameObject ) is not { Health: > 0 } patient ) return;

		var heal = MathF.Min( MathF.Min( 10, charge ), patient.MaxHealth - patient.Health );
		if ( heal <= 0 ) return;
		var spent = (int)heal;
		if ( Inventory.TakeAmmo( PrimaryAmmoType, spent ) != spent ) return;

		_nextTreatment = PrimaryDelay;
		patient.Health += heal;
		if ( healer.Health < healer.MaxHealth ) healer.Health = MathF.Min( healer.MaxHealth, healer.Health + spent * 0.25f );
		ContentMount?.NpcAudio.Emit( "items/medshot5.wav", Owner.WorldPosition, attenuation: 0.8f );
		using ( Rpc.FilterInclude( Owner.Network.Owner ?? Connection.Local ) )
			PlayTreatment( "shortuse" );
	}

	Component.IDamageable FindPatient( GameObject target )
	{
		if ( target.GetComponentInParent<PlayerController>() is { } player )
			return player == Owner ? null : MapState.PlayerVitals( player );
		var npc = target.GetComponentInParent<BaseNpc>();
		return npc.IsValid() && npc.GetRelationship( Owner.GameObject ).Disposition != Disposition.Hostile
			? npc
			: null;
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void PlayTreatment( string sequence )
	{
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( sequence, idleAfter: sequence == "shortuse" ? 1 : null );
		PostWeaponEvent( WeaponEvents.Attack );
	}
}

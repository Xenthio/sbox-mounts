using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Title( "Knife" ), Category( "Counter-Strike" )]
[WeaponPickup( "knife", null, 0, Bucket = 2, Weight = 0, Mount = "cstrike" )]
public sealed class Knife : KnifeWeapon;

[Title( "Knife" ), Category( "Counter-Strike: Condition Zero" )]
[WeaponPickup( "knife", null, 0, Bucket = 2, Weight = 0, Mount = "czero" )]
public sealed class ConditionZeroKnife : KnifeWeapon;

[Hide]
public abstract class KnifeWeapon : CrowbarWeapon
{
	const float SlashReach = 48;
	const float StabReach = 32;
	bool _alternateSlash;

	internal override (string Name, float? Delay) IdleAnimation => ("idle", 20);

	internal override void Configure()
	{
		UsesAmmo = UsesClips = false;
		PrimaryAutomatic = SecondaryAutomatic = true;
		Handedness = WeaponHandedness.Right;
		HoldType = WeaponHolds.Knife;
		AttackDamageTags = ["bullet", "nevergib"];
		Ballistics = new() { Damage = 15, Range = SlashReach, Radius = 0 };
	}

	protected override void OnEquipped()
	{
		base.OnEquipped();
		_alternateSlash = false;
		PlaySample( "weapons/knife_deploy1.wav", 0.3f );
	}

	public override void PrimaryAttack()
	{
		var aim = AimRay;
		var hit = TraceSwing( aim );
		SetNextPrimaryFire( hit.Hit ? 0.4f : 0.35f );
		SetNextSecondaryFire( 0.5f );
		var sequence = _alternateSlash ? "midslash2" : "midslash1";
		_alternateSlash = !_alternateSlash;
		ResolveStrike( hit, Ballistics.Damage, sequence, false );
	}

	public override void SecondaryAttack()
	{
		var aim = AimRay;
		var hit = TraceMelee( aim, StabReach );
		SetNextFire( hit.Hit ? 1.1f : 1f );
		ResolveStrike( hit, StabDamage( aim.Forward, hit.GameObject ), hit.Hit ? "stab" : "stab_miss", true );
	}

	static float StabDamage( Vector3 forward, GameObject target )
	{
		if ( target?.GetComponentInParent<PlayerController>() is not { } victim ) return 65;
		var victimForward = Rotation.FromYaw( victim.EyeAngles.yaw ).Forward;
		return forward.WithZ( 0 ).Normal.Dot( victimForward ) > 0.8f ? 195 : 65;
	}

	void ResolveStrike( SceneTraceResult hit, float damage, string sequence, bool stab )
	{
		var shot = new ShotEffect( hit.EndPosition, hit.Hit, hit.Normal, hit.GameObject, hit.Surface );
		var flesh = HitFlesh( shot );
		if ( hit.Hit ) ShootBullet( hit, damage, 0 );

		var sample = !hit.Hit ? $"knife_slash{Game.Random.Int( 1, 2 )}"
			: !flesh ? "knife_hitwall1"
			: stab ? "knife_stab"
			: $"knife_hit{Game.Random.Int( 1, 4 )}";
		var pitch = !hit.Hit ? 0.94f : flesh ? 1 : Game.Random.Int( 98, 101 ) / 100f;
		PlayStrike( sequence, sample, pitch );
	}

	[Rpc.Broadcast( NetFlags.OwnerOnly )]
	void PlayStrike( string sequence, string sample, float pitch )
	{
		PostWeaponEvent( WeaponEvents.Attack );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( sequence, idleAfter: 2 );
		PlaySample( $"weapons/{sample}.wav", pitch: pitch );
	}
}

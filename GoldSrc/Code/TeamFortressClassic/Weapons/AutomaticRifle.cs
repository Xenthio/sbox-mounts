using GoldSrc.HalfLife;

namespace GoldSrc.TeamFortressClassic;

[Title( "Automatic Rifle" ), Category( "Team Fortress Classic" )]
[WeaponPickup( "tfc_sniper", "buckshot", 200, EntityName = "tf_weapon_autorifle", PickupModel = "p_sniper", Bucket = 2, Weight = 10, Mount = "tfc" )]
public sealed class AutomaticRifle : BulletWeapon
{
	internal override string WorldModelPath => "models/p_sniper.mdl";
	internal override string DeploySequence => "autodraw";
	internal override string ShotSequence => "autofire";
	internal override string ShotSound => "weapons/sniper.wav";
	internal override float? IdleAfterShot => 0.2f;
	internal override (string Name, float? Delay) IdleAnimation => ("autoidle", 12.5f);
	internal override int PlayerMuzzleFlash => 40;
	internal override Vector2 PelletSpread => Vector2.Zero;

	internal override void Configure()
	{
		HoldType = WeaponHolds.Rifle;
		UsesClips = false;
		StartingAmmo = 5;
		PrimaryAutomatic = true;
		PrimaryDelay = 0.1f;
		AttackDamageTags = ["bullet"];
		Ballistics = new() { Damage = 8, Range = 8192, Radius = 0 };
	}

	public override Ray AimRay => Owner.IsValid() ? OffsetLaunchRay( Vector3.Left * 2 ) : base.AimRay;

	public override bool CanSecondaryAttack() => false;

	public override void DryFire()
	{
		PlaySample( "weapons/357_cock1.wav", 0.8f );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( "idle", idleAfter: 1 );
		SetNextPrimaryFire( 5 );
	}

	internal override Vector2 SamplePelletOffset() => Vector2.Zero;

	internal override void DrawShotTrail( Vector3 start, Vector3 end ) { }

	internal override void PlayShotSound() => PlaySample( ShotSound, 0.9f );
}

using GoldSrc.HalfLife;

namespace GoldSrc.TeamFortressClassic;

[Title( "Railgun" ), Category( "Team Fortress Classic" )]
[WeaponPickup( "tfc_railgun", "9mm", 200, EntityName = "tf_weapon_railgun", PickupModel = "p_9mmhandgun", Bucket = 1, Mount = "tfc" )]
public sealed class Railgun : NailWeapon
{
	internal override string WorldModelPath => "models/p_9mmhandgun.mdl";
	internal override string DeploySequence => "draw";
	internal override string ShotSequence => "fire";
	internal override (string Name, float? Delay) IdleAnimation => ("idle", 12.5f);
	internal override float? IdleAfterShot => 12.5f;
	internal override float Recoil => 2;

	internal override void Configure()
	{
		base.Configure();
		HoldType = WeaponHolds.Pistol;
		Handedness = WeaponHandedness.Right;
		PrimaryDelay = 0.4f;
	}

	internal override bool Release( Ray ray )
	{
		NpcProjectile.Fire<RailSlug>( Owner.GameObject, null, 25, null, ProjectileOrigin( ray ), ray.Forward, 1500 );
		return true;
	}

	internal override void PlayLaunchSound()
	{
		base.PlayLaunchSound();
		PlaySample( "weapons/railgun.wav", 0.9f );
	}
}

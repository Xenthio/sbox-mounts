using GoldSrc.HalfLife;

namespace GoldSrc.TeamFortressClassic;

[Hide]
public abstract class NailWeapon : ProjectileWeapon
{
	internal virtual float Recoil => 1;

	internal override string DeploySequence => "deploy";
	internal override float? IdleAfterShot => 10;
	internal override (string Name, float? Delay) IdleAnimation => ("idle1", 15);

	internal override void Configure()
	{
		HoldType = WeaponHolds.Smg;
		UsesClips = false;
		StartingAmmo = 50;
		PrimaryDelay = 0.1f;
	}

	internal override bool Release( Ray ray ) => ReleaseNail( ray, 9 );

	internal bool ReleaseNail( Ray ray, float damage )
	{
		var model = ContentMount?.FindMountPath( "models/nail.mdl" );
		if ( model is null ) return false;
		NpcProjectile.Fire<Nail>( Owner.GameObject, $"mount://{Definition.Mount}/{model}.vmdl", damage, null, ProjectileOrigin( ray ), ray.Forward, 1000 );
		return true;
	}

	internal static Vector3 ProjectileOrigin( Ray ray )
	{
		var basis = Rotation.LookAt( ray.Forward );
		return ray.Position + basis.Right * 2 - basis.Up * 4;
	}

	internal override void PlayLaunchSound()
	{
		base.PlayLaunchSound();
		if ( !IsProxy && Owner is { ThirdPerson: false } )
			MeleeViewPunch.Apply( Scene.Camera, new Angles( -Recoil, 0, 0 ) );
	}
}

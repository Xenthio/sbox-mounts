using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

[Title( "Freezethrower" ), Category( "Scientist Slaughterhouse" )]
[WeaponPickup( "egon", "uranium", 999, ClassName = "freezethrower", Bucket = 3, Weight = 20, Mount = "ssh" )]
public sealed class Freezethrower : ProjectileWeapon
{
	internal override string ShotSequence => $"fire{Game.Random.Int( 1, 4 )}";
	internal override float? IdleAfterShot => 1;
	internal override (string Name, float? Delay) IdleAnimation =>
		Game.Random.Int( 0, 1 ) == 0 ? ("idle1", Game.Random.Float( 10, 15 )) : ("fidget1", 3);
	public override Ray AimRay => Owner.IsValid() ? OffsetLaunchRay( new Vector3( 0, -3, -12 ) ) : base.AimRay;

	internal override void Configure()
	{
		HoldType = WeaponHolds.Rifle;
		UsesClips = false;
		StartingAmmo = 100;
		PrimaryDelay = 0.3f;
	}

	internal override bool Release( Ray ray )
	{
		if ( WaterBody.Find( Scene, Owner.EyePosition ).IsValid() )
		{
			ClickUnderwater();
			return false;
		}
		NpcProjectile.Fire<FreezeSpark>( Owner.GameObject, "mount://ssh/ssh/sprites/qtorp.spr", 10, null, ray.Position, ray.Forward, 1000 );
		return true;
	}

	internal override void PlayLaunchSound() => PlaySample( "weapons/displacer_start.wav", attenuation: 0.8f );

	public override void DryFire() => Click( 0.5f );

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ClickUnderwater() => Click( 0.15f );

	void Click( float cooldown )
	{
		SetNextFire( cooldown );
		PlaySample( "weapons/357_cock1.wav", attenuation: 0.8f );
	}
}

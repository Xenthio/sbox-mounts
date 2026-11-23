using GoldSrc.HalfLife;

namespace GoldSrc.CrackLife;

[Title( "Rock" ), Category( "Crack-Life: Campaign Mode" )]
[WeaponPickup( "rock", null, 0, Bucket = 0, Weight = 0, Mount = "clcampaign" )]
public sealed class Rock : CampaignThrowable
{
	internal override string ShotSequence => "throw2";

	internal override void Configure()
	{
		UsesAmmo = UsesClips = false;
		HoldType = WeaponHolds.Throwable;
		PrimaryDelay = 0.75f;
	}

	internal override bool Release( Ray ray )
	{
		if ( ContentMount is not { } mount || mount.FindMountPath( "models/w_rock.mdl" ) is not { } path ) return false;
		TimedGrenade.Spawn<ThrownRock>( Owner.GameObject, LaunchOrigin( ray ), ray.Forward.Normal * LaunchSpeed,
			float.PositiveInfinity, "Thrown rock", $"mount://{mount.Ident}/{path}.vmdl",
			rock =>
			{
				IgnoreThrower( rock );
				rock.GameObject.Tags.Add( "removable" );
			}, effectsMount: mount );
		return true;
	}
}

[Hide]
public sealed class ThrownRock : TimedGrenade
{
	// The rock uses the grenade collision callback without the fuse or tumble think.
	protected override void OnFixedUpdate() { }
}

[Hide]
public abstract class CampaignThrowable : ProjectileWeapon
{
	protected const float LaunchSpeed = 800;
	internal override (string Name, float? Delay) IdleAnimation => ("idle", null);
	public override Ray AimRay => Owner.IsValid() ? Owner.EyeTransform.ForwardRay : base.AimRay;
	public override bool CanSecondaryAttack() => false;

	protected Vector3 LaunchOrigin( Ray aim )
	{
		// GoldSrc launches at 32 units above the player's origin, just above the standing eye height.
		var origin = aim.Position + Vector3.Up * (Owner.IsDucking ? 20 : 4);
		return BulletTrace( new Ray( origin, aim.Forward.Normal ), 34, 2 ).Run().EndPosition;
	}

	protected void IgnoreThrower( TimedGrenade projectile )
	{
		if ( Owner.Body.IsValid() ) projectile.AddComponent<PhysicsFilter>().Body = Owner.Body.GameObject;
	}
}

using GoldSrc.HalfLife;

namespace GoldSrc.CrackLife;

[Title( "Rocket Launcher" ), Category( "Crack-Life" )]
[WeaponPickup( "rpg", "rockets", 5, Bucket = 3, Weight = 20, SkipAutoSelect = true, Mount = "cracklife" )]
public sealed class RocketLauncher : CrackLifeRocketLauncher
{
	internal override int FreshPickupRounds => Networking.MaxPlayers > 1 ? 2 : 1;
	internal override PlayerRocket LaunchRocket( Ray ray ) => FireRocket<CrackLifeRocket>( ray, 300 );
}

[Title( "Rocket Launcher" ), Category( "Crack-Life: Campaign Mode" )]
[WeaponPickup( "rpg", "rockets", 125, Bucket = 3, Weight = 20, SkipAutoSelect = true, Mount = "clcampaign" )]
public sealed class CampaignRocketLauncher : CrackLifeRocketLauncher
{
	internal override void Configure()
	{
		base.Configure();
		UsesClips = false;
		StartingAmmo = Networking.MaxPlayers > 1 ? 50 : 25;
		PrimaryDelay = 0.4f;
	}

	internal override PlayerRocket LaunchRocket( Ray ray ) => FireRocket<CampaignRocket>( ray, 110 );
}

[Hide]
public abstract class CrackLifeRocketLauncher : GuidedRocketLauncher
{
	public override Ray AimRay => Owner.IsValid() ? OffsetLaunchRay( new Vector3( 16, -8, -8 ) ) : base.AimRay;

	internal override void Configure()
	{
		base.Configure();
		ReloadTime = 2;
	}

	internal override void PlayLaunchSound()
	{
		base.PlayLaunchSound();
		if ( !IsProxy && Owner is { ThirdPerson: false } )
			MeleeViewPunch.Apply( Scene.Camera, new Angles( -5, 0, 0 ) );
	}

	protected PlayerRocket FireRocket<T>( Ray ray, float damage ) where T : PlayerRocket, new()
	{
		if ( ContentMount is not { } mount || mount.FindMountPath( "models/rpgrocket.mdl" ) is not { } path ) return null;
		return NpcProjectile.Fire<T>( Owner.GameObject, $"mount://{mount.Ident}/{path}.vmdl", damage, null, ray.Position, ray.Forward, 250 );
	}
}

[Hide]
public sealed class CrackLifeRocket : PlayerRocket
{
	internal override GameMount EffectMount => Sandbox.Mounting.Directory.Get( "cracklife" ) as GameMount;
	internal override GameMount BlastMount => EffectMount;
}

[Hide]
public sealed class CampaignRocket : PlayerRocket
{
	internal override GameMount EffectMount => Sandbox.Mounting.Directory.Get( "clcampaign" ) as GameMount;
	internal override GameMount BlastMount => EffectMount;
}

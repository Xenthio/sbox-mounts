namespace GoldSrc.HalfLife;

[Title( "Rocket Launcher" ), Category( "Half-Life" )]
[WeaponPickup( "rpg", "rockets", 5, Bucket = 3, Weight = 20, SkipAutoSelect = true )]
public sealed class RocketLauncher : GuidedRocketLauncher
{
	internal override int FreshPickupRounds => Networking.MaxPlayers > 1 ? 2 : 1;

	internal override PlayerRocket LaunchRocket( Ray ray )
	{
		var damage = Networking.MaxPlayers > 1 ? 120 : 100;
		return NpcProjectile.Fire<PlayerRocket>( Owner.GameObject, "mount://hl1/valve/models/rpgrocket.mdl.vmdl",
			damage, null, ray.Position, ray.Forward, 250 );
	}
}

[Hide]
public abstract partial class GuidedRocketLauncher : ProjectileWeapon
{
	bool HasLoadedRocket => !UsesPrimaryClip || Clip1 > 0;
	internal override string DeploySequence => HasLoadedRocket ? "draw1" : "draw2";
	internal override string ShotSequence => "fire";
	internal override float? IdleAfterShot => 1.5f;
	internal override float IdleAfterReloadStart => Game.Random.Float( 10, 15 );
	internal override bool CanIdle => Ammo1 > 0;
	internal override void PlayIdle( StudioWeaponView view )
	{
		if ( !LaserEnabled && Game.Random.Float( 0, 1 ) > 0.75f &&
			view.Play( HasLoadedRocket ? "fidget" : "fidget2", idleAfter: 3 ) ) return;
		// The HD model has no empty-launcher fidget.
		view.Play( HasLoadedRocket ? "idle" : "idle2", idleAfter: 6 );
	}
	internal override void PlayLaunchSound()
	{
		PlaySample( "weapons/rocketfire1.wav", 0.9f );
		PlaySample( "weapons/glauncher.wav", 0.7f );
	}
	internal override Vector3 WorldMuzzleOffset( bool hd ) => hd ? new( 23.4f, 0.3f, 5.4f ) : new( 22.5f, 1.4f, 4.5f );

	internal override void Configure()
	{
		HoldType = WeaponHolds.Shoulder;
		PrimaryDelay = 1.5f;
		SecondaryDelay = 0.2f;
		ClipMaxSize = 1;
	}

	internal abstract PlayerRocket LaunchRocket( Ray ray );

	internal override bool Release( Ray ray )
	{
		var rocket = LaunchRocket( ray );
		if ( !rocket.IsValid() ) return false;
		rocket.Launcher = this;
		ActiveRockets++;
		return true;
	}
}

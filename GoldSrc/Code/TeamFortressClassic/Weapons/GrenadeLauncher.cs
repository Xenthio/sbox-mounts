using GoldSrc.HalfLife;

namespace GoldSrc.TeamFortressClassic;

[Title( "Grenade Launcher" ), Category( "Team Fortress Classic" )]
[WeaponPickup( "tfgl", "rockets", 50, EntityName = "tf_weapon_gl", PickupModel = "p_glauncher", Bucket = 4, Weight = 15, Mount = "tfc" )]
public sealed class GrenadeLauncher : FortressLauncher
{
	internal override string WorldModelPath => "models/p_glauncher.mdl";
}

[Title( "Pipebomb Launcher" ), Category( "Team Fortress Classic" )]
[WeaponPickup( "tfgl", "rockets", 50, EntityName = "tf_weapon_pl", PickupModel = "p_glauncher2", Bucket = 4, Weight = 15, Mount = "tfc" )]
public sealed class PipebombLauncher : FortressLauncher
{
	internal override bool LaunchesPipes => true;
	internal override string WorldModelPath => "models/p_glauncher2.mdl";

	public override bool CanSecondaryAttack() => IsActive && Owner.IsValid() && NextSecondaryFire <= 0;

	public override void SecondaryAttack()
	{
		SetNextSecondaryFire( 0.15f );
		DetonatePipes();
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void DetonatePipes()
	{
		if ( !IsActive || !Owner.IsValid() ) return;
		foreach ( var pipe in FortressGrenade.PipesOwnedBy( Owner.GameObject ).Where( pipe => pipe.IsArmed ).ToArray() )
			pipe.Detonate();
	}
}

[Hide]
public abstract class FortressLauncher : ProjectileWeapon
{
	internal virtual bool LaunchesPipes => false;
	string Prefix => LaunchesPipes ? "p" : "g";
	internal override string DeploySequence => Prefix + "draw";
	internal override string ShotSequence => Prefix + "fire";
	internal override string ShotSound => "weapons/glauncher.wav";
	internal override string ReloadSequence => Prefix + "reload1";
	internal override float IdleAfterReloadStart => ReloadStartTime;
	internal override float? IdleAfterShot => 0.6f;
	internal override (string Name, float? Delay) IdleAnimation => (Prefix + "idle", 3);

	internal override void Configure()
	{
		HoldType = WeaponHolds.Rifle;
		ClipMaxSize = 6;
		DeployTime = 0.5f;
		PrimaryAutomatic = true;
		PrimaryDelay = 0.6f;
		IncrementalReloading = true;
		ReloadStartTime = 0.1f;
		ReloadTime = 2f / 3f;
	}

	public override Ray AimRay => Owner.IsValid() ? Owner.EyeTransform.ForwardRay : base.AimRay;
	public override bool CanSecondaryAttack() => false;
	public override bool CanReload() => NextPrimaryFire <= 0 && base.CanReload();

	public override void DryFire()
	{
		if ( CanReload() ) { Reload(); return; }
		PlaySample( "weapons/357_cock1.wav" );
		SetNextPrimaryFire( 1 );
	}

	internal override bool Release( Ray ray )
	{
		if ( ContentMount?.FindFile( "models/pipebomb.mdl" ) is not { } modelPath ) return false;
		var basis = Rotation.LookAt( ray.Forward );
		var origin = Owner.EyePosition + basis.Right * 8 - basis.Up * 16;
		var velocity = basis.Forward * 600 + basis.Up * (200 + Game.Random.Float( -10, 10 )) + basis.Right * Game.Random.Float( -10, 10 );
		FortressGrenade.Launch( Owner.GameObject, origin, basis, velocity, LaunchesPipes, $"mount://tfc/{modelPath}.vmdl" );
		return true;
	}

	internal override void PlayLaunchSound()
	{
		PlaySample( ShotSound, volume: 0.7f );
		if ( !IsProxy && Owner is { ThirdPerson: false } )
			MeleeViewPunch.Apply( Scene.Camera, new Angles( -2, 0, 0 ) );
	}

	protected override void OnReloadInserted()
	{
		base.OnReloadInserted();
		PlaySample( "items/clipinsert1.wav" );
	}

	protected override void OnReloadFinished()
	{
		base.OnReloadFinished();
		if ( !IsActive ) return;
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( Prefix + "reload2", idleAfter: 1.5f );
		PlaySample( "items/cliprelease1.wav" );
	}
}

using GoldSrc.HalfLife;

namespace GoldSrc.CryOfFear;

[Title( "Glock 19" ), Category( "Cry of Fear" )]
[WeaponPickup( "glock", "glock", 150, Bucket = 4, ModelDirectory = "models/weapons/glock", Mount = "cof" )]
public sealed class Glock : BulletWeapon
{
	internal override Vector2 PelletSpread => Vector2.One * 0.01745f;
	internal override int PlayerMuzzleFlash => 1;
	internal override string WorldModelPath => "police/models/weapons/glock/p_glock.mdl";
	internal override string ShotSound => "weapons_new/glock/glock_fire.wav";
	internal override string DeploySequence => Clip1 == 0 ? "draw_noshot" : "draw";
	internal override string ShotSequence => Clip1 == 0 ? "shoot_empty_1" : "shoot_1";
	internal override (string Name, float? Delay) IdleAnimation => (Clip1 == 0 ? "idle1_noshot" : "idle1", null);
	internal override float? IdleAfterShot => 1;
	[Sync( SyncFlags.FromHost )] bool ReloadingFromEmpty { get; set; }

	internal override string ReloadSequence => ReloadingFromEmpty ? "reload_noshot" : "reload";
	internal override float IdleAfterReloadStart => ReloadingFromEmpty ? 3.5f : 2.6f;

	internal override void Configure()
	{
		HoldType = WeaponHolds.Pistol;
		Handedness = WeaponHandedness.Right;
		PrimaryAutomatic = false;
		PrimaryDelay = 0.1f;
		AutoReload = false;
		CanCancelReload = false;
		ReloadTime = 2.6f;
		ClipMaxSize = 15;
		AttackDamageTags = ["bullet"];
		Ballistics = new() { Damage = 13, Range = 8192, Radius = 0 };
	}

	public override void Reload()
	{
		if ( Networking.IsHost && CanReload() )
		{
			ReloadingFromEmpty = Clip1 == 0;
			ReloadTime = IdleAfterReloadStart;
		}
		base.Reload();
	}

	protected override void OnReloadStarted()
	{
		base.OnReloadStarted();
		// The magazine is discarded when the reload begins.
		if ( Networking.IsHost ) Clip1 = 0;
	}

	protected override void OnShootEffects( ShotEffect shot )
	{
		base.OnShootEffects( shot );
		if ( !shot.NoEvents ) EjectShells( 1, false );
	}

	public override void DryFire()
	{
		PlaySample( "weapons_new/glock/weapon_fire_empty.wav" );
		SetNextFire( 0.5f );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( "shoot_noshot", idleAfter: 0.5f );
	}
}

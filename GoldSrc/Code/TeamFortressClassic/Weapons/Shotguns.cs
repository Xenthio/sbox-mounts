using GoldSrc.HalfLife;

namespace GoldSrc.TeamFortressClassic;

[Title( "Single Shotgun" ), Category( "Team Fortress Classic" )]
[WeaponPickup( "tfc_12gauge", "buckshot", 200, EntityName = "tf_weapon_shotgun", PickupModel = "p_smallshotgun", Bucket = 1, Weight = 15, Mount = "tfc" )]
public sealed class SingleShotgun : FortressShotgun
{
	internal override string WorldModelPath => "models/p_smallshotgun.mdl";
	internal override string ShotSound => "weapons/sbarrel1.wav";
}

[Title( "Super Shotgun" ), Category( "Team Fortress Classic" )]
[WeaponPickup( "tfc_shotgun", "buckshot", 200, EntityName = "tf_weapon_supershotgun", PickupModel = "p_shotgun", Bucket = 2, Weight = 15, Mount = "tfc" )]
public sealed class SuperShotgun : FortressShotgun
{
	internal override int ShellsPerShot => 2;
	internal override int PlayerMuzzleFlash => 41;
	internal override string WorldModelPath => "models/p_shotgun.mdl";
	internal override string ShotSound => "weapons/shotgn2.wav";
	internal override string ShotSequence => "shoot_big";

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 16;
		PrimaryDelay = 0.7f;
		ReloadStartTime = 0.475f;
		ReloadTime = 0.375f;
		Ballistics = Ballistics with { Pellets = 14 };
	}

	// The engine requests one reserve round per incremental insert. This weapon loads pairs.
	protected override int ReloadFillTarget => Clip1 + System.Math.Min( (PrimaryClipSize - Clip1) / 2, Ammo1 / 2 ) * 2;

	protected override int TakeReserveAmmo( BaseAmmoResource ammoType, int amount )
	{
		if ( !IsReloading || ammoType != PrimaryAmmoType ) return base.TakeReserveAmmo( ammoType, amount );
		if ( Ammo1 >= 2 ) return base.TakeReserveAmmo( ammoType, amount * 2 );
		CancelReload();
		return 0;
	}
}

[Hide]
public abstract class FortressShotgun : BulletWeapon
{
	TimeUntil? _pumpAt;

	internal virtual int ShellsPerShot => 1;
	internal override int PlayerMuzzleFlash => 31;
	internal override Vector2 PelletSpread => Vector2.One * 0.04f;
	internal override string ReloadSequence => "start_reload";
	internal override float IdleAfterReloadStart => ReloadStartTime;
	internal override float? IdleAfterShot => 5;
	internal override (string Name, float? Delay) IdleAnimation => (Game.Random.Int( 0, 1 ) == 0 ? "sm_idle" : "idle4", null);

	internal override void Configure()
	{
		HoldType = WeaponHolds.Shotgun;
		PrimaryAutomatic = true;
		ClipMaxSize = 8;
		PrimaryDelay = 0.5f;
		IncrementalReloading = true;
		ReloadStartTime = 0.35f;
		ReloadTime = 0.25f;
		AttackDamageTags = ["bullet"];
		Ballistics = new() { Damage = 4, Range = 2048, Radius = 0, Pellets = 6 };
	}

	public override bool HasPrimaryAmmo() => !IsHeld || Clip1 >= ShellsPerShot;
	public override bool CanSecondaryAttack() => false;
	public override bool CanReload() => NextPrimaryFire <= 0 && base.CanReload();

	public override void PrimaryAttack()
	{
		if ( IsHeld && !HasPrimaryAmmo() ) return;
		if ( IsHeld && ShellsPerShot > 1 ) TakePrimaryAmmo( ShellsPerShot - 1 );
		base.PrimaryAttack();
	}

	public override void DryFire()
	{
		if ( CanReload() ) Reload();
		else PlaySample( "weapons/357_cock1.wav" );
		SetNextPrimaryFire( 0.15f );
	}

	internal override void DrawShotTrail( Vector3 start, Vector3 end ) { }

	protected override void OnShootEffects( ShotEffect shot )
	{
		base.OnShootEffects( shot );
		if ( shot.NoEvents ) return;
		EjectShells( ShellsPerShot, true );
		_pumpAt = PrimaryDelay;
		if ( !IsProxy && Owner is { ThirdPerson: false } )
			MeleeViewPunch.Apply( Scene.Camera, new Angles( ShellsPerShot == 1 ? -2 : -4, 0, 0 ) );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( _pumpAt is not { } due || due > 0 ) return;
		_pumpAt = null;
		if ( IsActive && !IsReloading ) PlaySample( "weapons/scock1.wav" );
	}

	protected override void OnHolstered()
	{
		_pumpAt = null;
		base.OnHolstered();
	}

	protected override void OnReloadInserted()
	{
		base.OnReloadInserted();
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( "reload" );
		PlaySample( Game.Random.Int( 0, 1 ) == 0 ? "weapons/reload1.wav" : "weapons/reload3.wav" );
	}

	protected override void OnReloadFinished()
	{
		base.OnReloadFinished();
		if ( !IsActive ) return;
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( "pump" );
		PlaySample( "weapons/scock1.wav" );
	}
}

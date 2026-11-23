using GoldSrc.HalfLife;

namespace GoldSrc.CryOfFear;

[Title( "Remington 870" ), Category( "Cry of Fear" )]
[WeaponPickup( "shotgun", "buckshot", 50, Bucket = 3, ModelDirectory = "models/weapons/shotgun", Mount = "cof" )]
public sealed class Shotgun : IronSightWeapon
{
	const float OpenTime = 0.5f;
	const float InsertTime = 0.6f;
	const float InsertInterval = 0.65f;
	TimeUntil? _nextInsert;
	TimeUntil? _shellEjection;
	bool _reloadStartedEmpty;

	internal override int FreshPickupRounds => 9;
	internal override int PlayerMuzzleFlash => 1;
	internal override string WorldModelPath => "police/models/weapons/shotgun/p_shotgun.mdl";
	internal override string ShotSound => "weapons_new/shotgun/shoot.wav";
	internal override string ShotSequence => Zoomed ? "iron_shoot" : "shoot1";
	internal override string ReloadSequence => "rel_start";
	internal override float IdleAfterReloadStart => OpenTime;
	internal override float? IdleAfterShot => Clip1 == 0 ? PrimaryDelay : 5;
	internal override (string Name, float? Delay) IdleAnimation => Zoomed ? ("iron_idle", null) : ("idle1", null);
	internal override Vector2 PelletSpread => Vector2.One * 0.06976f;

	internal override void Configure()
	{
		HoldType = WeaponHolds.Shotgun;
		ClipMaxSize = 5;
		PrimaryAutomatic = false;
		PrimaryDelay = DeployTime = 1;
		SecondaryDelay = 0.25f;
		AutoReload = false;
		IncrementalReloading = true;
		ReloadStartTime = OpenTime + InsertTime;
		ReloadTime = InsertInterval;
		AttackDamageTags = ["bullet"];
		Ballistics = new() { Damage = 5, Pellets = 8, Range = 2048, Radius = 0 };
	}

	public override void PrimaryAttack()
	{
		if ( !HasPrimaryAmmo() ) return;
		base.PrimaryAttack();
		if ( !IsProxy && Owner is { ThirdPerson: false } ) MeleeViewPunch.Apply( Scene.Camera, new Angles( -5, 0, 0 ) );
	}

	internal override void DrawShotTrail( Vector3 start, Vector3 end ) { }

	public override void DryFire()
	{
		PlaySample( "weapons_new/glock/weapon_fire_empty.wav" );
		SetNextFire( 0.15f );
	}

	protected override void OnShootEffects( ShotEffect shot )
	{
		base.OnShootEffects( shot );
		if ( !shot.NoEvents ) _shellEjection = 11f / 30;
	}

	protected override void OnReloadStarted()
	{
		_reloadStartedEmpty = Clip1 == 0;
		_nextInsert = OpenTime;
		base.OnReloadStarted();
	}

	protected override void OnReloadInserted()
	{
		base.OnReloadInserted();
		if ( Clip1 < ClipMaxSize && Ammo1 > 0 ) _nextInsert = InsertInterval - InsertTime;
	}

	protected override void OnReloadFinished()
	{
		base.OnReloadFinished();
		_nextInsert = null;
		if ( !IsActive ) return;

		var pump = _reloadStartedEmpty && Clip1 > 0;
		var sequence = pump ? "rel_end_noshot" : "rel_end";
		var time = pump ? 0.65f : 0.5f;
		SetNextFire( time );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( sequence, idleAfter: time );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( _nextInsert is { } insert && insert <= 0 )
		{
			_nextInsert = null;
			if ( IsReloading ) ViewModel?.GetComponent<StudioWeaponView>()?.Play( "rel_insert" );
		}
		if ( _shellEjection is { } eject && eject <= 0 )
		{
			_shellEjection = null;
			if ( IsActive ) EjectShells( 1, true, "models/weapons/shotgunshell.mdl" );
		}
	}
}

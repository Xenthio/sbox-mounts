using GoldSrc.HalfLife;

namespace GoldSrc.CrackLife;

[Title( "Sniper Rifle" ), Category( "Crack-Life: Campaign Mode" )]
[WeaponPickup( "sniper", "snipars", 100, ClassName = "snipars", Bucket = 2, Weight = 15, Mount = "clcampaign" )]
public sealed class Sniper : BulletWeapon
{
	TimeUntil? _reloadSound;

	internal override int PlayerMuzzleFlash => 41;
	internal override Vector2 PelletSpread => Vector2.One * 0.00873f;
	internal override float ZoomFieldOfView => 40;
	internal override string ShotSequence => "fire";
	internal override string ShotSound => "weapons/sniper_shoot";
	internal override (string Name, float? Delay) IdleAnimation => ("idle", 170f / 30);

	internal override void Configure()
	{
		HoldType = WeaponHolds.Rifle;
		ClipMaxSize = 10;
		PrimaryDelay = 1.5f;
		SecondaryDelay = 0.5f;
		ReloadTime = 2;
		AttackDamageTags = ["bullet"];
		Ballistics = new() { Damage = 40, Range = 8192, Radius = 0 };
	}

	public override bool CanSecondaryAttack() => NextSecondaryFire <= 0 && !IsReloading;

	public override void PrimaryAttack()
	{
		if ( Owner.IsValid() && WaterBody.Find( Scene, Owner.EyePosition ).IsValid() )
		{
			DryFire();
			return;
		}

		base.PrimaryAttack();
	}

	public override void DryFire()
	{
		PlaySample( "weapons/357_cock1.wav" );
		SetNextPrimaryFire( 0.15f );
	}

	internal override void DrawShotTrail( Vector3 start, Vector3 end ) { }

	protected override void OnReloadStarted()
	{
		base.OnReloadStarted();
		_reloadSound = 1.5f;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( _reloadSound is not { } due || due > 0 ) return;
		_reloadSound = null;
		if ( IsReloading && IsActive ) PlaySample( "weapons/357_reload1.wav", Game.Random.Float( 0.8f, 0.9f ) );
	}

	protected override void OnHolstered()
	{
		_reloadSound = null;
		base.OnHolstered();
	}
}

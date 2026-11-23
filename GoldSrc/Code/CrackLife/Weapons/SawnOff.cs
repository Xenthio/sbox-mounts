using GoldSrc.HalfLife;

namespace GoldSrc.CrackLife;

[Title( "Sawn-off Shotgun" ), Category( "Crack-Life: Campaign Mode" )]
[WeaponPickup( "sawnoff", "buckshot", 125, Bucket = 2, Weight = 15, Mount = "clcampaign" )]
public sealed class SawnOff : BulletWeapon
{
	internal override int PlayerMuzzleFlash => 51;
	internal override Vector2 PelletSpread => Vector2.One * 0.17365f;
	internal override string ShotSound => "weapons/sshotgun_shoot.wav";
	internal override (string Name, float? Delay) IdleAnimation => ("idle", null);

	internal override void Configure()
	{
		HoldType = WeaponHolds.Shotgun;
		ClipMaxSize = 4;
		PrimaryDelay = 0.5f;
		ReloadTime = 1.3f;
		AttackDamageTags = ["bullet"];
		Ballistics = new() { Damage = 5, Pellets = 18, Range = 2048, Radius = 0 };
	}

	public override bool CanSecondaryAttack() => false;

	public override void PrimaryAttack()
	{
		if ( Owner.IsValid() && WaterBody.Find( Scene, Owner.EyePosition ).IsValid() )
		{
			PlaySample( "weapons/sshotgun_cock3.wav", 0.8f );
			SetNextPrimaryFire( 0.15f );
			return;
		}

		if ( IsHeld && Clip1 > 1 ) TakePrimaryAmmo();
		base.PrimaryAttack();
	}

	public override void DryFire()
	{
		if ( AutoReload && CanReload() ) Reload();
		PlaySample( "weapons/sshotgun_cock3.wav", 0.8f );
		SetNextPrimaryFire( 0.15f );
	}

	internal override void DrawShotTrail( Vector3 start, Vector3 end ) { }

	protected override void OnReloadStarted()
	{
		base.OnReloadStarted();
		PlaySample( "weapons/sshotgun_reload.wav" );
	}
}

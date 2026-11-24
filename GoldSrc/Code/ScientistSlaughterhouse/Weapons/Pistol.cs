using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

[Title( "9mm Handgun" ), Category( "Scientist Slaughterhouse" )]
[WeaponPickup( "9mmhandgun", "9mm", 400, Bucket = 1, Weight = 10, PreviewPitch = 90, Mount = "ssh" )]
public sealed class Pistol : ClassicPistol
{
	internal override float ShotDamage => 8;

	public override Ray AimRay => Owner.IsValid() ? Owner.EyeTransform.ForwardRay : base.AimRay;

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 18;
	}

	// GoldSrc starts with the old 17-round check, then fills to the mod's 18-round capacity.
	public override bool CanReload() => Clip1 < 17 && base.CanReload();

	protected override void Think()
	{
		base.Think();
		if ( !Input.Pressed( "reload" ) || Input.Down( "attack1" ) || Input.Down( "attack2" ) ||
			Clip1 != ClipMaxSize || Ammo1 <= 0 || IsReloading || NextPrimaryFire > 0 || NextSecondaryFire > 0 ) return;

		// At 18 rounds the original check is negative, so it animates without moving any ammo.
		SetNextFire( ReloadTime );
		OnReloadStarted();
	}
}

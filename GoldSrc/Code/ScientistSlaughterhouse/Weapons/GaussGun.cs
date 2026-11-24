using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

[Title( "Tau Cannon" ), Category( "Scientist Slaughterhouse" )]
[WeaponPickup( "gauss", "uranium", 999, Bucket = 5, Weight = 20, Mount = "ssh" )]
public sealed class GaussGun : ClassicGaussGun
{
	public override bool CanSecondaryAttack() => NextPrimaryFire <= 0 && base.CanSecondaryAttack();

	internal override void Configure()
	{
		base.Configure();
		StartingAmmo = 100;
	}
}

using GoldSrc.HalfLife;

namespace GoldSrc.CrackLife;

[Title( "Tau Cannon" ), Category( "Crack-Life" )]
[WeaponPickup( "gauss", "uranium", 100, Bucket = 3, Weight = 20, Mount = "cracklife" )]
public sealed class GaussGun : CrackLifeGaussGun;

[Title( "Tau Cannon" ), Category( "Crack-Life: Campaign Mode" )]
[WeaponPickup( "gauss", "uranium", 999, Bucket = 3, Weight = 20, Mount = "clcampaign" )]
public sealed class CampaignGaussGun : CrackLifeGaussGun
{
	internal override void Configure()
	{
		base.Configure();
		StartingAmmo = 999;
	}
}

[Hide]
public abstract class CrackLifeGaussGun : ClassicGaussGun
{
	internal override void Configure()
	{
		base.Configure();
		Ballistics = Ballistics with { Damage = 50 };
	}
}

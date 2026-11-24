using GoldSrc.HalfLife;

namespace GoldSrc.CrackLife;

[Title( "Gluon Gun" ), Category( "Crack-Life" )]
[WeaponPickup( "egon", "uranium", 100, Bucket = 3, Weight = 20, Mount = "cracklife" )]
public sealed class GluonGun : ClassicGluonGun
{
	internal override string ObserverEffectSprite => "shit";

	internal override void Configure()
	{
		base.Configure();
		Ballistics = Ballistics with { Damage = 9999 };
	}
}

[Title( "Gluon Gun" ), Category( "Crack-Life: Campaign Mode" )]
[WeaponPickup( "egon", "uranium", 999, Bucket = 3, Weight = 20, Mount = "clcampaign" )]
public sealed class CampaignGluonGun : ClassicGluonGun
{
	internal override void Configure()
	{
		base.Configure();
		StartingAmmo = 999;
		Ballistics = Ballistics with { Damage = 666 };
	}
}

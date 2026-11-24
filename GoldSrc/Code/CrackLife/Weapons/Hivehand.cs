using GoldSrc.HalfLife;

namespace GoldSrc.CrackLife;

[Title( "Hivehand" ), Category( "Crack-Life" )]
[WeaponPickup( "hgun", "hornets", 8, ClassName = "hornetgun", Bucket = 3, Weight = 10, Mount = "cracklife" )]
public sealed class Hivehand : CrackLifeHivehand;

[Title( "Hivehand" ), Category( "Crack-Life: Campaign Mode" )]
[WeaponPickup( "hgun", "hornets", 8, ClassName = "hornetgun", Bucket = 3, Weight = 10, Mount = "clcampaign" )]
public sealed class CampaignHivehand : CrackLifeHivehand
{
	internal override float RechargeDelay => 0.05f;

	internal override void Configure()
	{
		base.Configure();
		PrimaryDelay = 0.025f;
	}
}

[Hide]
public abstract class CrackLifeHivehand : ClassicHivehand
{
	internal override float RechargeInterval => 0.5f;
	internal override float EmptyPrimaryDelay => 0;
	internal override float EmptySecondaryDelay => 0;
}

using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Title( "Bren Light Machine Gun" ), Category( "Day of Defeat" )]
[WeaponPickup( "bren", "ammo_55mm", 180, Bucket = 2, Weight = 20, Mount = "dod" )]
public sealed class Bren : BipodRifle
{
	internal override int FreshPickupRounds => 180;
	internal override int PlayerMuzzleFlash => 41;
	internal override string CarriedModel => "p_brenbu";
	internal override string DeployedModel => "p_brenbd";
	internal override float BipodTime => 1.575f;
	internal override float CarriedReloadTime => 3.6f;
	internal override float DeployedReloadTime => 4.2f;

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 30;
		DeployTime = 1;
		PrimaryDelay = 0.12f;
	}
}

using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Title( "Browning Automatic Rifle" ), Category( "Day of Defeat" )]
[WeaponPickup( "bar", "ammo_55mm", 260, Bucket = 2, Weight = 20, Mount = "dod" )]
public sealed class Bar : BipodRifle
{
	internal override int FreshPickupRounds => 260;
	internal override int PlayerMuzzleFlash => 61;
	internal override string CarriedModel => "p_barbu";
	internal override string DeployedModel => "p_barbd";
	internal override float BipodTime => 1.5f;
	internal override float CarriedReloadTime => 3.72f;

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 20;
		DeployTime = 1.5f;
		PrimaryDelay = 0.12f;
	}
}

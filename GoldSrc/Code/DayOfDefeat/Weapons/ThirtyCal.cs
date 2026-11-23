using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Title( "Browning .30 Cal" ), Category( "Day of Defeat" )]
[WeaponPickup( "30cal", "ammo_66mm_us", 300, Bucket = 2, Weight = 40, Mount = "dod" )]
public sealed class ThirtyCal : BeltFedMachineGun
{
	internal override int FreshPickupRounds => 300;
	internal override float CarriedReloadTime => 5;
	internal override string CarriedModel => "p_30cal";
	internal override string DeployedModel => CarriedModel;
	internal override string DeploySequence => "downtoup";

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 150;
	}
}

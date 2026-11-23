using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Title( "FG42" ), Category( "Day of Defeat" )]
[WeaponPickup( "fg42", "ammo_55mm", 180, Bucket = 2, Weight = 20, Mount = "dod" )]
public sealed class Fg42 : BipodRifle
{
	internal override int FreshPickupRounds => 180;
	internal override int PlayerMuzzleFlash => 61;
	internal override string CarriedModel => "p_fg42bu";
	internal override string DeployedModel => "p_fg42bd";
	internal override float BipodTime => 1.3f;
	internal override float CarriedReloadTime => 3.85f;


	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 20;
		DeployTime = 1;
		PrimaryDelay = 0.085f;
	}
}

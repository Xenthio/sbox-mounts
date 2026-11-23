using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Title( "M3 Grease Gun" ), Category( "Day of Defeat" )]
[WeaponPickup( "greasegun", "ammo_44mm", 210, Bucket = 2, Weight = 20, Mount = "dod" )]
public sealed class GreaseGun : DayOfDefeatFirearm
{
	internal override int FreshPickupRounds => 210;
	internal override int PlayerMuzzleFlash => 31;
	internal override float StandingSpread => 0.025f;
	internal override string WorldModelPath => "models/p_grease.mdl";
	internal override string ShotSequence => $"shoot{Game.Random.Int( 1, 2 )}";

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 30;
		ReloadTime = 4.1f;
		HoldType = WeaponHolds.Smg;
		PrimaryAutomatic = true;
		PrimaryDelay = 0.15f;
	}
}

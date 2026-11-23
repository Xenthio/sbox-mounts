using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Title( "M1 Carbine" ), Category( "Day of Defeat" )]
[WeaponPickup( "m1carbine", "ammo_66mm", 165, PickupModel = "w_m1carb", Bucket = 2, Weight = 20, Mount = "dod" )]
public sealed class M1Carbine : CarbineWeapon
{
	internal override string WorldModelPath => "models/p_m1carb.mdl";
}

[Title( "M1A1 Paratrooper Carbine" ), Category( "Day of Defeat" )]
[WeaponPickup( "fcarb", "ammo_66mm", 165, Bucket = 2, Weight = 20, Mount = "dod" )]
public sealed class ParatrooperCarbine : CarbineWeapon;

[Hide]
public abstract class CarbineWeapon : DayOfDefeatFirearm
{
	internal override int FreshPickupRounds => 165;
	internal override int PlayerMuzzleFlash => 31;
	internal override int BrassBodyGroup => 1;
	internal override float StandingSpread => 0.019f;
	internal override string ShotSound => "weapons/carbine_shoot.wav";
	internal override (string Name, float? Delay) IdleAnimation => ("idle", 2.5f);

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 15;
		HoldType = WeaponHolds.Rifle;
		DeployTime = 1;
		PrimaryDelay = 0.1f;
		ReloadTime = 3.14f;
	}
}

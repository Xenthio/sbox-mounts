using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Title( "Luger P08" ), Category( "Day of Defeat" )]
[WeaponPickup( "luger", "ammo_12mm", 24, Bucket = 1, Weight = 10, Mount = "dod" )]
public sealed class Luger : DayOfDefeatFirearm
{
	internal override int FreshPickupRounds => 24;
	internal override int PlayerMuzzleFlash => 31;
	internal override string ShotSequence => Clip1 == 0 ? "shoot_empty" : "shoot";
	internal override string ReloadSequence => Clip1 == 0 ? "reload_empty" : "reload";

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 8;
		ReloadTime = 2.1f;
	}
}

using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Title( "Colt M1911A1" ), Category( "Day of Defeat" )]
[WeaponPickup( "colt", "ammo_12mm", 24, Bucket = 1, Weight = 10, Mount = "dod" )]
public sealed class Colt : DayOfDefeatFirearm
{
	internal override int FreshPickupRounds => 21;
	internal override int PlayerMuzzleFlash => 21;
	internal override string ShotSequence => Clip1 == 0 ? "shoot_empty" : "shoot1";
	internal override string ReloadSequence => Clip1 == 0 ? "reload_noshoot" : "reload";
	internal override (string Name, float? Delay) IdleAnimation => (Clip1 == 0 ? "empty_idle" : "idle", 6);

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 7;
		ReloadTime = 2.75f;
	}
}

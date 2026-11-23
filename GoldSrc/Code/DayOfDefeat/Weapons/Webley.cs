using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Title( "Webley" ), Category( "Day of Defeat" )]
[WeaponPickup( "webley", "ammo_12mm", 24, Bucket = 1, Weight = 10, Mount = "dod" )]
public sealed class Webley : DayOfDefeatFirearm
{
	internal override int FreshPickupRounds => 18;
	internal override int PlayerMuzzleFlash => 41;
	internal override bool EjectsBrass => false;
	internal override (string Name, float? Delay) IdleAnimation => ("idle", 2.5f);

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 6;
		ReloadTime = 4f;
	}
}

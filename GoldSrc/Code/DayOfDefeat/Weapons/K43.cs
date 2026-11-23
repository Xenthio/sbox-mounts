using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Title( "Karabiner 43" ), Category( "Day of Defeat" )]
[WeaponPickup( "k43", "ammo_66mm", 80, Bucket = 2, Weight = 20, Mount = "dod" )]
public sealed class K43 : DayOfDefeatRifle
{
	internal override int FreshPickupRounds => 80;
	internal override float StandingSpread => 0.019f;
	internal override int PlayerMuzzleFlash => 51;
	internal override string MeleeSequence => "smash";
	internal override string ShotSequence => $"shoot{Game.Random.Int( 1, 2 )}";

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 10;
		ReloadTime = 4.4f;
		Ballistics = Ballistics with { Damage = 120 };
	}
}

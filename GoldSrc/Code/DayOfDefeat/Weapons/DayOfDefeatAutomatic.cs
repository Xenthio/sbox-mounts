namespace GoldSrc.DayOfDefeat;

[Hide]
public abstract class DayOfDefeatAutomatic : DayOfDefeatFirearm
{
	internal override int FreshPickupRounds => 210;
	internal override int PlayerMuzzleFlash => 31;
	internal override float StandingSpread => 0.025f;
	internal override string ShotSequence => $"shoot{Game.Random.Int( 1, 2 )}";

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 30;
		HoldType = WeaponHolds.Smg;
		PrimaryAutomatic = true;
	}
}

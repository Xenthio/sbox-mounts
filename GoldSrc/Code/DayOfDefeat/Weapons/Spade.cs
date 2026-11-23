using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Title( "Entrenching Spade" ), Category( "Day of Defeat" )]
[WeaponPickup( "spade", null, 0, Weight = 0, Mount = "dod" )]
public sealed class Spade : DayOfDefeatMelee
{
	internal override void Configure()
	{
		base.Configure();
		HoldType = WeaponHolds.Melee;
	}
}

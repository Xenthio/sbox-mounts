using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Title( "Five-SeveN" ), Category( "Counter-Strike" )]
[WeaponPickup( "fiveseven", "57mm", 100, Bucket = 1, Weight = 5, Mount = "cstrike" )]
public sealed class FiveSeven : FiveSevenWeapon;

[Title( "Five-SeveN" ), Category( "Counter-Strike: Condition Zero" )]
[WeaponPickup( "fiveseven", "57mm", 100, Bucket = 1, Weight = 5, Mount = "czero" )]
public sealed class ConditionZeroFiveSeven : FiveSevenWeapon;

[Hide]
public abstract class FiveSevenWeapon : CounterStrikePistol
{
	internal override PistolAccuracy Accuracy => new( 0.92f, 0.725f, 0.275f, 0.25f );
	internal override float RangeModifier => 0.885f;

	internal override void Configure()
	{
		base.Configure();
		PrimaryDelay = 0.15f;
		ReloadTime = 2.7f;
		ClipMaxSize = 20;
		Ballistics = new() { Damage = 20, Range = 4096, Radius = 0 };
	}
}

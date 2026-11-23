using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Title( "SG 552" ), Category( "Counter-Strike" )]
[WeaponPickup( "sg552", "556nato", 90, Bucket = 0, Weight = 25, Mount = "cstrike" )]
public sealed class Sg552 : Sg552Weapon;

[Title( "SG 552" ), Category( "Counter-Strike: Condition Zero" )]
[WeaponPickup( "sg552", "556nato", 90, Bucket = 0, Weight = 25, Mount = "czero" )]
public sealed class ConditionZeroSg552 : Sg552Weapon;

[Hide]
public abstract class Sg552Weapon : ScopedCounterStrikeRifle
{
	internal override float AccuracyDivisor => 220;
	internal override float AirborneSpread => 0.45f;
	internal override float MovingSpread => 0.075f;
	internal override float RangeModifier => 0.955f;
	internal override float? IdleAfterShot => 2;

	internal override void Configure()
	{
		base.Configure();
		ReloadTime = 3;
		Ballistics = new() { Damage = 33, Range = 8192, Radius = 0 };
	}
}

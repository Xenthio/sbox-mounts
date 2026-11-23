using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Title( "AK-47" ), Category( "Counter-Strike" )]
[WeaponPickup( "ak47", "762nato", 90, Bucket = 0, Weight = 25, Mount = "cstrike" )]
public sealed class Ak47 : Ak47Weapon;

[Title( "AK-47" ), Category( "Counter-Strike: Condition Zero" )]
[WeaponPickup( "ak47", "762nato", 90, Bucket = 0, Weight = 25, Mount = "czero" )]
public sealed class ConditionZeroAk47 : Ak47Weapon;

[Hide]
public abstract class Ak47Weapon : CounterStrikeRifle
{
	internal override float AccuracyDivisor => 200;
	internal override float AccuracyOffset => 0.35f;
	internal override float MaxAccuracy => 1.25f;
	internal override float StandingSpread => 0.0275f;
	internal override float MovingPenalty => 0.04f;
	internal override float RangeModifier => 0.98f;
	internal override (string Name, float? Delay) IdleAnimation => ("idle1", 20);

	internal override void Configure()
	{
		base.Configure();
		PrimaryDelay = 0.0955f;
		ReloadTime = 2.45f;
		Ballistics = new() { Damage = 36, Range = 8192, Radius = 0 };
	}
}

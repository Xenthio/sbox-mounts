using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Title( "AUG" ), Category( "Counter-Strike" )]
[WeaponPickup( "aug", "556nato", 90, Bucket = 0, Weight = 25, Mount = "cstrike" )]
public sealed class Aug : AugWeapon;

[Title( "AUG" ), Category( "Counter-Strike: Condition Zero" )]
[WeaponPickup( "aug", "556nato", 90, Bucket = 0, Weight = 25, Mount = "czero" )]
public sealed class ConditionZeroAug : AugWeapon;

[Hide]
public abstract class AugWeapon : ScopedCounterStrikeRifle
{
	internal override float AccuracyDivisor => 215;
	internal override float ReloadAccuracy => 0;
	internal override float RangeModifier => 0.96f;

	internal override void Configure()
	{
		base.Configure();
		ReloadTime = 3.3f;
		Ballistics = new() { Damage = 32, Range = 8192, Radius = 0 };
	}
}

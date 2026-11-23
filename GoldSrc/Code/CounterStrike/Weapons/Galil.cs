using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Title( "Galil" ), Category( "Counter-Strike" )]
[WeaponPickup( "galil", "556nato", 90, Bucket = 0, Weight = 25, Mount = "cstrike" )]
public sealed class Galil : GalilWeapon;

[Title( "Galil" ), Category( "Counter-Strike: Condition Zero" )]
[WeaponPickup( "galil", "556nato", 90, Bucket = 0, Weight = 25, Mount = "czero" )]
public sealed class ConditionZeroGalil : GalilWeapon;

[Hide]
public abstract class GalilWeapon : CounterStrikeRifle
{
	internal override float AccuracyDivisor => 200;
	internal override float AccuracyOffset => 0.35f;
	internal override float MaxAccuracy => 1.25f;
	internal override float StandingSpread => 0.0375f;
	internal override float AirborneSpread => 0.3f;
	internal override float MovingPenalty => 0.04f;
	internal override float RangeModifier => 0.98f;
	internal override float? IdleAfterShot => 1.28f;
	internal override string ShotSound => "weapons/galil-";

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 35;
		PrimaryDelay = 0.0875f;
		ReloadTime = 2.45f;
		Ballistics = new() { Damage = 30, Range = 8192, Radius = 0 };
	}
}

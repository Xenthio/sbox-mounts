using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Title( "P90" ), Category( "Counter-Strike" )]
[WeaponPickup( "p90", "57mm", 100, Bucket = 0, Weight = 25, Mount = "cstrike" )]
public sealed class P90 : P90Weapon;

[Title( "P90" ), Category( "Counter-Strike: Condition Zero" )]
[WeaponPickup( "p90", "57mm", 100, Bucket = 0, Weight = 25, Mount = "czero" )]
public sealed class ConditionZeroP90 : P90Weapon;

[Hide]
public abstract class P90Weapon : CounterStrikeSmg
{
	internal override float AccuracyDivisor => 175;
	internal override float AccuracyOffset => 0.45f;
	internal override bool QuadraticAccuracy => true;
	internal override float StandingSpread => 0.045f;
	internal override float AirborneSpread => 0.3f;
	internal override float MovingSpread => 0.115f;
	internal override float MovingSpreadSpeed => 170;
	internal override float RangeModifier => 0.885f;
	internal override string BrassModel => "models/rshell.mdl";
	internal override (string Name, float? Delay) IdleAnimation => ("idle", 20);

	internal override KickPattern Kickback => new(
		Airborne: new( 0.9f, 0.45f, 0.35f, 0.04f, 5.25f, 3.5f, 4 ),
		Moving: new( 0.45f, 0.3f, 0.2f, 0.0275f, 4, 2.25f, 7 ),
		Crouched: new( 0.275f, 0.2f, 0.125f, 0.02f, 3, 1, 9 ),
		Standing: new( 0.3f, 0.225f, 0.125f, 0.02f, 3.25f, 1.25f, 8 ) );

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 50;
		PrimaryDelay = 0.066f;
		ReloadTime = 3.4f;
		Ballistics = new() { Damage = 21, Range = 8192, Radius = 0 };
	}
}

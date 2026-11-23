using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Title( "MP5 Navy" ), Category( "Counter-Strike" )]
[WeaponPickup( "mp5", "9mm", 120, ClassName = "mp5navy", Bucket = 0, Weight = 25, Mount = "cstrike" )]
public sealed class Mp5Navy : Mp5NavyWeapon;

[Title( "MP5 Navy" ), Category( "Counter-Strike: Condition Zero" )]
[WeaponPickup( "mp5", "9mm", 120, ClassName = "mp5navy", Bucket = 0, Weight = 25, Mount = "czero" )]
public sealed class ConditionZeroMp5Navy : Mp5NavyWeapon;

[Hide]
public abstract class Mp5NavyWeapon : CounterStrikeSmg
{
	internal override float InitialAccuracy => 0;
	internal override float AccuracyDivisor => 220.1f;
	internal override float AccuracyOffset => 0.45f;
	internal override float MaxAccuracy => 0.75f;
	internal override bool QuadraticAccuracy => true;
	internal override bool IntegerAccuracyDivision => false;
	internal override float StandingSpread => 0.04f;
	internal override float AirborneSpread => 0.2f;
	internal override float RangeModifier => 0.84f;

	internal override KickPattern Kickback => new(
		Airborne: new( 0.9f, 0.475f, 0.35f, 0.0425f, 5, 3, 6 ),
		Moving: new( 0.5f, 0.275f, 0.2f, 0.03f, 3, 2, 10 ),
		Crouched: new( 0.225f, 0.15f, 0.1f, 0.015f, 2, 1, 10 ),
		Standing: new( 0.25f, 0.175f, 0.125f, 0.02f, 2.25f, 1.25f, 10 ) );

	internal override void Configure()
	{
		base.Configure();
		PrimaryDelay = 0.075f;
		ReloadTime = 2.63f;
		Ballistics = new() { Damage = 26, Range = 8192, Radius = 0 };
	}
}

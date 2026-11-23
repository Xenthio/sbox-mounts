using System;
using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Title( "M249" ), Category( "Counter-Strike" )]
[WeaponPickup( "m249", "556natobox", 200, Bucket = 0, Weight = 25, Mount = "cstrike" )]
public sealed class M249 : M249Weapon;

[Title( "M249" ), Category( "Counter-Strike: Condition Zero" )]
[WeaponPickup( "m249", "556natobox", 200, Bucket = 0, Weight = 25, Mount = "czero" )]
public sealed class ConditionZeroM249 : M249Weapon;

[Hide]
public abstract class M249Weapon : CounterStrikeAutomatic
{
	internal override float AccuracyDivisor => 175;
	internal override float AccuracyOffset => 0.4f;
	internal override float MaxAccuracy => 0.9f;
	internal override float StandingSpread => 0.03f;
	internal override float MovingSpread => 0.095f;
	internal override float MovingPenalty => 0.045f;
	internal override float AirborneSpread => 0.5f;
	internal override float RangeModifier => 0.97f;
	internal override float ShotAttenuation => 0.52f;
	internal override float? IdleAfterShot => 1.6f;
	internal override string ShotSequence => Game.Random.Int( 1, 2 ) == 1 ? "shoot1" : "shoot2";

	internal override KickPattern Kickback => new(
		Airborne: new( 1.8f, 0.65f, 0.45f, 0.125f, 5, 3.5f, 8 ),
		Moving: new( 1.1f, 0.5f, 0.3f, 0.06f, 4, 3, 8 ),
		Crouched: new( 0.75f, 0.325f, 0.25f, 0.025f, 3.5f, 2.5f, 9 ),
		Standing: new( 0.8f, 0.35f, 0.3f, 0.03f, 3.75f, 3, 9 ) );

	internal override float FiringAccuracy( int shots ) =>
		MathF.Min( MaxAccuracy, shots * shots * shots / (int)AccuracyDivisor + AccuracyOffset );

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 100;
		PrimaryDelay = 0.1f;
		ReloadTime = 4.7f;
		Ballistics = new() { Damage = 32, Range = 8192, Radius = 0 };
	}
}

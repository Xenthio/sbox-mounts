using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Title( "UMP45" ), Category( "Counter-Strike" )]
[WeaponPickup( "ump45", "45acp", 100, Bucket = 0, Weight = 25, Mount = "cstrike" )]
public sealed class Ump45 : Ump45Weapon;

[Title( "UMP45" ), Category( "Counter-Strike: Condition Zero" )]
[WeaponPickup( "ump45", "45acp", 100, Bucket = 0, Weight = 25, Mount = "czero" )]
public sealed class ConditionZeroUmp45 : Ump45Weapon;

[Hide]
public abstract class Ump45Weapon : CounterStrikeSmg
{
	internal override float InitialAccuracy => 0;
	internal override float AccuracyDivisor => 210;
	internal override float AccuracyOffset => 0.5f;
	internal override bool QuadraticAccuracy => true;
	internal override float StandingSpread => 0.04f;
	internal override float AirborneSpread => 0.24f;
	internal override float RangeModifier => 0.82f;

	internal override KickPattern Kickback => new(
		Airborne: new( 0.125f, 0.65f, 0.55f, 0.0475f, 5.5f, 4, 10 ),
		Moving: new( 0.55f, 0.3f, 0.225f, 0.03f, 3.5f, 2.5f, 10 ),
		Crouched: new( 0.25f, 0.175f, 0.125f, 0.02f, 2.25f, 1.25f, 10 ),
		Standing: new( 0.275f, 0.2f, 0.15f, 0.0225f, 2.5f, 1.5f, 10 ) );

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 25;
		PrimaryDelay = 0.1f;
		ReloadTime = 3.5f;
		Ballistics = new() { Damage = 30, Range = 8192, Radius = 0 };
	}
}

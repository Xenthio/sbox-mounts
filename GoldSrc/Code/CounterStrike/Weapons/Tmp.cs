using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Title( "TMP" ), Category( "Counter-Strike" )]
[WeaponPickup( "tmp", "9mm", 120, Bucket = 0, Weight = 25, Mount = "cstrike" )]
public sealed class Tmp : TmpWeapon;

[Title( "TMP" ), Category( "Counter-Strike: Condition Zero" )]
[WeaponPickup( "tmp", "9mm", 120, Bucket = 0, Weight = 25, Mount = "czero" )]
public sealed class ConditionZeroTmp : TmpWeapon;

[Hide]
public abstract class TmpWeapon : CounterStrikeSmg
{
	internal override float AccuracyDivisor => 200;
	internal override float AccuracyOffset => 0.55f;
	internal override float MaxAccuracy => 1.4f;
	internal override float StandingSpread => 0.03f;
	internal override float AirborneSpread => 0.25f;
	internal override float RangeModifier => 0.85f;
	internal override float ShotAttenuation => 1.6f;
	internal override string ShotSequence => "shoot";

	internal override KickPattern Kickback => new(
		Airborne: new( 1.1f, 0.5f, 0.35f, 0.045f, 4.5f, 3.5f, 6 ),
		Moving: new( 0.8f, 0.4f, 0.2f, 0.03f, 3, 2.5f, 7 ),
		Crouched: new( 0.7f, 0.35f, 0.125f, 0.025f, 2.5f, 2, 10 ),
		Standing: new( 0.725f, 0.375f, 0.15f, 0.025f, 2.75f, 2.25f, 9 ) );

	internal override void Configure()
	{
		base.Configure();
		HoldType = WeaponHolds.Pistol;
		PrimaryDelay = 0.07f;
		ReloadTime = 2.12f;
		Ballistics = new() { Damage = 20, Range = 8192, Radius = 0 };
	}
}

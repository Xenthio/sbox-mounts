using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Title( "MAC-10" ), Category( "Counter-Strike" )]
[WeaponPickup( "mac10", "45acp", 100, Bucket = 0, Weight = 25, Mount = "cstrike" )]
public sealed class Mac10 : Mac10Weapon;

[Title( "MAC-10" ), Category( "Counter-Strike: Condition Zero" )]
[WeaponPickup( "mac10", "45acp", 100, Bucket = 0, Weight = 25, Mount = "czero" )]
public sealed class ConditionZeroMac10 : Mac10Weapon;

[Hide]
public abstract class Mac10Weapon : CounterStrikeSmg
{
	internal override float InitialAccuracy => 0.15f;
	internal override float ReloadAccuracy => 0;
	internal override float AccuracyDivisor => 200;
	internal override float AccuracyOffset => 0.6f;
	internal override float MaxAccuracy => 1.65f;
	internal override float StandingSpread => 0.03f;
	internal override float AirborneSpread => 0.375f;
	internal override float RangeModifier => 0.82f;
	internal override float ShotAttenuation => 0.72f;
	internal override float IdleAfterReloadStart => Definition.Mount == "czero" ? 3.25f : base.IdleAfterReloadStart;

	internal override KickPattern Kickback => new(
		Airborne: new( 1.3f, 0.55f, 0.4f, 0.05f, 4.75f, 3.75f, 5 ),
		Moving: new( 0.9f, 0.45f, 0.25f, 0.035f, 3.5f, 2.75f, 7 ),
		Crouched: new( 0.75f, 0.4f, 0.175f, 0.03f, 2.75f, 2.5f, 10 ),
		Standing: new( 0.775f, 0.425f, 0.2f, 0.03f, 3, 2.75f, 9 ) );

	internal override void Configure()
	{
		base.Configure();
		HoldType = WeaponHolds.Pistol;
		PrimaryDelay = 0.07f;
		ReloadTime = 3.15f;
		Ballistics = new() { Damage = 29, Range = 8192, Radius = 0 };
	}
}

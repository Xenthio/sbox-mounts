using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Title( "Desert Eagle" ), Category( "Counter-Strike" )]
[WeaponPickup( "deagle", "50ae", 35, Bucket = 1, Weight = 7, Mount = "cstrike" )]
public sealed class DesertEagle : DesertEagleWeapon;

[Title( "Desert Eagle" ), Category( "Counter-Strike: Condition Zero" )]
[WeaponPickup( "deagle", "50ae", 35, Bucket = 1, Weight = 7, Mount = "czero" )]
public sealed class ConditionZeroDesertEagle : DesertEagleWeapon;

[Hide]
public abstract class DesertEagleWeapon : CounterStrikePistol
{
	internal override PistolAccuracy Accuracy => new( 0.9f, 0.55f, 0.4f, 0.35f );
	internal override float MovingSpread => 0.25f;
	internal override float CrouchingSpread => 0.115f;
	internal override float StandingSpread => 0.13f;
	internal override float RangeModifier => 0.81f;
	internal override float? IdleAfterShot => 1.8f;

	internal override void Configure()
	{
		base.Configure();
		PrimaryDelay = 0.225f;
		ReloadTime = 2.2f;
		ClipMaxSize = 7;
		Ballistics = new() { Damage = 54, Range = 4096, Radius = 0 };
	}
}

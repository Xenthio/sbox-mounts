using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Title( "Scout" ), Category( "Counter-Strike" )]
[WeaponPickup( "scout", "762nato", 90, Bucket = 0, Weight = 30, Mount = "cstrike" )]
public sealed class Scout : ScoutWeapon;

[Title( "Scout" ), Category( "Counter-Strike: Condition Zero" )]
[WeaponPickup( "scout", "762nato", 90, Bucket = 0, Weight = 30, Mount = "czero" )]
public sealed class ConditionZeroScout : ScoutWeapon;

[Hide]
public abstract class ScoutWeapon : BoltActionSniper
{
	internal override float BrassDelay => 0.56f;
	internal override float RangeModifier => 0.98f;
	internal override string ShotSequence => "shoot";
	internal override string ShotSound => "weapons/scout_fire-1.wav";

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 10;
		DeployTime = PrimaryDelay = 1.25f;
		ReloadTime = 2;
		Ballistics = new() { Damage = 75, Range = 8192, Radius = 0 };
	}

	internal override float CalculateSpread( bool grounded, bool crouched, float speed, bool zoomed )
	{
		var stance = !grounded ? 0.2f : speed > 170 ? 0.075f : crouched ? 0 : 0.007f;
		return stance + (zoomed ? 0 : HipFireSpread);
	}
}

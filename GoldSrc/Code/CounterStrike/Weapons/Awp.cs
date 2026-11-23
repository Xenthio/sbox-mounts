using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Title( "AWP" ), Category( "Counter-Strike" )]
[WeaponPickup( "awp", "338magnum", 30, Bucket = 0, Weight = 30, Mount = "cstrike" )]
public sealed class Awp : AwpWeapon;

[Title( "AWP" ), Category( "Counter-Strike: Condition Zero" )]
[WeaponPickup( "awp", "338magnum", 30, Bucket = 0, Weight = 30, Mount = "czero" )]
public sealed class ConditionZeroAwp : AwpWeapon;

[Hide]
public abstract class AwpWeapon : BoltActionSniper
{
	internal override float SecondScopeFov => 10;
	internal override float BrassDelay => 0.55f;
	internal override float HipFireSpread => 0.08f;
	internal override float RangeModifier => 0.99f;
	internal override float? IdleAfterShot => 2;
	internal override float IdleAfterReloadStart => 2.9f;
	internal override (string Name, float? Delay) IdleAnimation => (Definition.Mount == "czero" ? "idle" : "idle1", 60);
	internal override string ShotSequence => $"shoot{Game.Random.Int( 1, 3 )}";
	internal override string ShotSound => "weapons/awp1.wav";

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 10;
		DeployTime = PrimaryDelay = 1.45f;
		ReloadTime = 2.5f;
		Ballistics = new() { Damage = 115, Range = 8192, Radius = 0 };
	}

	internal override float CalculateSpread( bool grounded, bool crouched, float speed, bool zoomed )
	{
		var stance = !grounded ? 0.85f : speed switch
		{
			> 140 => 0.25f,
			> 10 => 0.1f,
			_ => crouched ? 0 : 0.001f
		};
		return stance + (zoomed ? 0 : HipFireSpread);
	}
}

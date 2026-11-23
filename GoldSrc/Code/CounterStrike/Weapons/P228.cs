using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Title( "P228" ), Category( "Counter-Strike" )]
[WeaponPickup( "p228", "357sig", 52, Bucket = 1, Weight = 5, Mount = "cstrike" )]
public sealed class P228 : P228Weapon;

[Title( "P228" ), Category( "Counter-Strike: Condition Zero" )]
[WeaponPickup( "p228", "357sig", 52, Bucket = 1, Weight = 5, Mount = "czero" )]
public sealed class ConditionZeroP228 : P228Weapon;

[Hide]
public abstract class P228Weapon : CounterStrikePistol
{
	internal override PistolAccuracy Accuracy => new( 0.9f, 0.6f, 0.325f, 0.3f );
	internal override float RangeModifier => 0.8f;
	internal override int FireSequenceCount => 3;

	internal override void Configure()
	{
		base.Configure();
		PrimaryDelay = 0.15f;
		ReloadTime = 2.7f;
		ClipMaxSize = 13;
		Ballistics = new() { Damage = 32, Range = 4096, Radius = 0 };
	}
}

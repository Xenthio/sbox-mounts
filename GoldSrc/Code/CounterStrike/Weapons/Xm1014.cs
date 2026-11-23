using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Title( "XM1014" ), Category( "Counter-Strike" )]
[WeaponPickup( "xm1014", "buckshot", 32, Bucket = 0, Weight = 20, Mount = "cstrike" )]
public sealed class Xm1014 : Xm1014Weapon;

[Title( "XM1014" ), Category( "Counter-Strike: Condition Zero" )]
[WeaponPickup( "xm1014", "buckshot", 32, Bucket = 0, Weight = 20, Mount = "czero" )]
public sealed class ConditionZeroXm1014 : Xm1014Weapon;

[Hide]
public abstract class Xm1014Weapon : CounterStrikeShotgun
{
	internal override string ShotSound => "weapons/xm1014-1.wav";
	internal override float ShotAttenuation => 0.52f;
	internal override Vector2 PelletSpread => Vector2.One * 0.0725f;
	internal override int GroundRecoil => 3;
	internal override int AirRecoil => 7;
	internal override float LoadedIdleDelay => 2.25f;
	internal override float EmptyIdleDelay => 0.75f;
	internal override string InsertSample => Game.Random.Int( 0, 1 ) == 0 ? "weapons/reload1.wav" : "weapons/reload3.wav";

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 7;
		PrimaryDelay = 0.25f;
		ReloadStartTime = 0.85f;
		ReloadTime = 0.3f;
		Ballistics = new() { Damage = 20, Pellets = 6, Range = 3048, Radius = 0 };
	}
}

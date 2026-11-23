using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Title( "M3" ), Category( "Counter-Strike" )]
[WeaponPickup( "m3", "buckshot", 32, Bucket = 0, Weight = 20, Mount = "cstrike" )]
public sealed class M3 : M3Weapon;

[Title( "M3" ), Category( "Counter-Strike: Condition Zero" )]
[WeaponPickup( "m3", "buckshot", 32, Bucket = 0, Weight = 20, Mount = "czero" )]
public sealed class ConditionZeroM3 : M3Weapon;

[Hide]
public abstract class M3Weapon : CounterStrikeShotgun
{
	internal override string ShotSound => "weapons/m3-1.wav";
	internal override float ShotAttenuation => 0.48f;
	internal override Vector2 PelletSpread => Vector2.One * 0.0675f;
	internal override int GroundRecoil => 4;
	internal override int AirRecoil => 8;
	internal override float LoadedIdleDelay => 2.5f;
	internal override float BrassDelay => 0.45f;
	internal override Vector3? BrassOffset => new Vector3( 16, 0, -9 );

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 8;
		PrimaryDelay = 0.875f;
		ReloadStartTime = 1;
		ReloadTime = 0.45f;
		Ballistics = new() { Damage = 20, Pellets = 9, Range = 3000, Radius = 0 };
	}
}

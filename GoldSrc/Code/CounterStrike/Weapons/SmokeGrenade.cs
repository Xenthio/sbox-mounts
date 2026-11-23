using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Title( "Smoke Grenade" ), Category( "Counter-Strike" )]
[WeaponPickup( "smokegrenade", "smokegrenade", 1, Bucket = 3, Weight = 1, Mount = "cstrike" )]
public sealed class SmokeGrenade : SmokeGrenadeWeapon;

[Title( "Smoke Grenade" ), Category( "Counter-Strike: Condition Zero" )]
[WeaponPickup( "smokegrenade", "smokegrenade", 1, Bucket = 3, Weight = 1, Mount = "czero" )]
public sealed class ConditionZeroSmokeGrenade : SmokeGrenadeWeapon;

[Hide]
public abstract class SmokeGrenadeWeapon : CounterStrikeGrenadeWeapon
{
	internal override void ThrowGrenade( Vector3 origin, Vector3 velocity ) =>
		SmokeGrenadeProjectile.Throw( Owner.GameObject, origin, velocity, ContentMount );
}

using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Title( "HE Grenade" ), Category( "Counter-Strike" )]
[WeaponPickup( "hegrenade", "hegrenade", 1, Bucket = 3, Weight = 2, Mount = "cstrike" )]
public sealed class HeGrenade : HeGrenadeWeapon;

[Title( "HE Grenade" ), Category( "Counter-Strike: Condition Zero" )]
[WeaponPickup( "hegrenade", "hegrenade", 1, Bucket = 3, Weight = 2, Mount = "czero" )]
public sealed class ConditionZeroHeGrenade : HeGrenadeWeapon;

[Hide]
public abstract class HeGrenadeWeapon : CounterStrikeGrenadeWeapon
{
	internal override void ThrowGrenade( Vector3 origin, Vector3 velocity ) =>
		HeGrenadeProjectile.Throw( Owner.GameObject, origin, velocity, ContentMount );
}

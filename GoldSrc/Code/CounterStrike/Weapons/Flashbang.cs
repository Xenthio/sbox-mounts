using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Title( "Flashbang" ), Category( "Counter-Strike" )]
[WeaponPickup( "flashbang", "flashbang", 2, Bucket = 3, Weight = 1, Mount = "cstrike" )]
public sealed class Flashbang : FlashbangWeapon;

[Title( "Flashbang" ), Category( "Counter-Strike: Condition Zero" )]
[WeaponPickup( "flashbang", "flashbang", 2, Bucket = 3, Weight = 1, Mount = "czero" )]
public sealed class ConditionZeroFlashbang : FlashbangWeapon;

[Hide]
public abstract class FlashbangWeapon : CounterStrikeGrenadeWeapon
{
	internal override void ThrowGrenade( Vector3 origin, Vector3 velocity ) =>
		FlashbangProjectile.Throw( Owner.GameObject, origin, velocity, ContentMount );
}

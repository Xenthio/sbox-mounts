using GoldSrc.HalfLife;

namespace GoldSrc.SvenCoop;

[Title( "Uzi" ), Category( "Sven Co-op" )]
[WeaponPickup( "uzi", "9mm", 250, Bucket = 2, Mount = "svencoop" )]
public sealed class Uzi : BulletWeapon
{
	internal override Vector2 PelletSpread => Vector2.One * 0.05234f;
	internal override int PlayerMuzzleFlash => 40;
	internal override float IdleAfterReloadStart => ReloadTime;
	internal override (string Name, float? Delay) IdleAnimation => ( $"idle{Game.Random.Int( 1, 3 )}", null );

	internal override void Configure()
	{
		HoldType = WeaponHolds.Pistol;
		Handedness = WeaponHandedness.Right;
		PrimaryAutomatic = true;
		PrimaryDelay = 0.1f;
		ReloadTime = 2.67f;
		ClipMaxSize = 32;
		AttackDamageTags = ["bullet"];
		Ballistics = new() { Damage = 10, Range = 8192, Radius = 0 };
	}

	internal override void PlayShotSound() => PlaySample( "weapons/uzi/shoot" );

	protected override void OnShootEffects( ShotEffect shot )
	{
		base.OnShootEffects( shot );
		if ( !shot.NoEvents ) EjectShells( 1, false );
	}
}

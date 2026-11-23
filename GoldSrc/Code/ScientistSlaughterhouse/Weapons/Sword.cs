using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

[Title( "Sword" ), Category( "Scientist Slaughterhouse" )]
[WeaponPickup( "sword", null, 0, Mount = "ssh" )]
public sealed class Sword : CrowbarWeapon
{
	internal override void Configure()
	{
		HoldType = WeaponHolds.Melee;
		UsesClips = UsesAmmo = false;
		PrimaryDelay = 0.5f;
		AttackDamageTags = ["club", "slash"];
		Ballistics = new() { Damage = 10, Range = 64, Radius = 0 };
	}

	public override void PrimaryAttack()
	{
		var variant = Game.Random.Int( 1, 3 );
		var hit = TraceSwing( AimRay );
		if ( hit.Hit )
		{
			ShootBullet( hit, Ballistics.Damage * (variant == 3 ? 5 : 3), 0, hitboxTags: false );
			SetNextPrimaryFire( 0.2f );
		}

		PostWeaponEvent( WeaponEvents.Attack );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( $"attack{variant}" );
		PlaySample( "weapons/cbar_miss1.wav" );
		if ( !hit.Hit ) return;

		var shot = new ShotEffect( hit.EndPosition, hit.Hit, hit.Normal, hit.GameObject, hit.Surface );
		if ( HitFlesh( shot ) )
		{
			PlaySample( "weapons/cbar_hitbod" );
			return;
		}

		PlaySample( "weapons/cbar_hit" );
		ImpactDecal.Place( hit.EndPosition, hit.Normal, $"shot{Game.Random.Int( 1, 5 )}", 10 );
	}
}

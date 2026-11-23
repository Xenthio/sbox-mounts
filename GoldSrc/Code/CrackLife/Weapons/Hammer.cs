using GoldSrc.HalfLife;

namespace GoldSrc.CrackLife;

[Title( "Hammer" ), Category( "Crack-Life: Campaign Mode" )]
[WeaponPickup( "hammer", null, 0, Bucket = 0, Weight = 0, Mount = "clcampaign" )]
public sealed class Hammer : ClassicCrowbar
{
	internal override float HitDelay => 1;
	internal override float SwingDamage( bool fresh ) => Ballistics.Damage;
	internal override (string Name, float? Delay) IdleAnimation => ("idle", null);

	internal override void Configure()
	{
		base.Configure();
		PrimaryDelay = 1;
		Ballistics = Ballistics with { Damage = 400 };
	}

	public override bool CanSecondaryAttack() => false;

	protected override void OnShootEffects( ShotEffect shot )
	{
		if ( !shot.NoEvents )
		{
			PostWeaponEvent( WeaponEvents.Attack );
			ViewModel?.GetComponent<StudioWeaponView>()?.Play( "attack" );
		}

		if ( Application.IsDedicatedServer || !shot.Hit ) return;
		if ( HitFlesh( shot ) )
		{
			PlaySample( "weapons/hammer_hitbod.wav" );
			return;
		}

		var volume = PlaySurfaceImpact( shot );
		if ( volume > 0 )
			PlaySample( "weapons/hammer_hit.wav", volume, Game.Random.Int( 98, 101 ) / 100f );
		QueueImpactDecal( shot, 1 );
	}
}

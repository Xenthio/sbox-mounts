using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Hide]
public abstract class DayOfDefeatMelee : CrowbarWeapon
{
	bool _alternateSlash;
	TimeUntil? _impactDelay;
	Vector3 _impactPosition;
	Vector3 _impactNormal;

	internal override (string Name, float? Delay) IdleAnimation => ("idle", null);

	internal override void Configure()
	{
		UsesAmmo = UsesClips = false;
		HoldType = WeaponHolds.Knife;
		PrimaryAutomatic = true;
		PrimaryDelay = 0.35f;
		DeployTime = 0.5f;
		Ballistics = new() { Damage = 60, Range = 48, Radius = 0 };
		AttackDamageTags = ["club", "bullet", "nevergib"];
	}

	public override bool CanSecondaryAttack() => false;

	public override void PrimaryAttack()
	{
		var hit = TraceSwing( AimRay );
		if ( hit.Hit ) ShootBullet( hit, Ballistics.Damage, 0, hitboxTags: false );
		SetNextPrimaryFire( hit.Hit ? 0.4f : PrimaryDelay );
		ShootEffects( new ShotEffect( hit.EndPosition, hit.Hit, hit.Normal, hit.GameObject, hit.Surface ) );
	}

	protected override void OnShootEffects( ShotEffect shot )
	{
		PostWeaponEvent( WeaponEvents.Attack );
		if ( Application.IsDedicatedServer ) return;

		ViewModel?.GetComponent<StudioWeaponView>()?.Play( _alternateSlash ? "slash2" : "slash1" );
		_alternateSlash = !_alternateSlash;
		if ( !shot.Hit )
			PlaySample( $"weapons/knife_slash{Game.Random.Int( 1, 2 )}.wav" );
		else if ( HitFlesh( shot ) )
			PlaySample( $"weapons/knife_hit{Game.Random.Int( 1, 4 )}.wav" );
		else
		{
			PlaySample( "weapons/knife_hitwall1.wav", pitch: Game.Random.Int( 98, 101 ) / 100f );
			_impactPosition = shot.HitPosition;
			_impactNormal = shot.Normal;
			_impactDelay = 0.2f;
		}
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( _impactDelay is not { } delay || delay > 0 ) return;
		_impactDelay = null;
		ImpactDecal.Place( _impactPosition, _impactNormal, $"shot{Game.Random.Int( 1, 5 )}", 10, ContentMount );
	}
}

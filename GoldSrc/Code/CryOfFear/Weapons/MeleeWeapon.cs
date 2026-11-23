using GoldSrc.HalfLife;

namespace GoldSrc.CryOfFear;

[Hide]
public abstract class MeleeWeapon : CrowbarWeapon
{
	TimeUntil? _strike;
	float _damage;

	internal abstract float StrikeDelay { get; }
	internal abstract Angles StrikePunch { get; }
	internal virtual bool SwingSoundAtImpact => true;
	internal virtual bool MarksWalls => false;

	string Sample( string action ) => $"weapons/{Definition.Model}/{Definition.Model}_{action}.wav";

	internal override void Configure()
	{
		UsesAmmo = UsesClips = false;
		PrimaryAutomatic = true;
		Handedness = WeaponHandedness.Right;
		DeployTime = 29f / 30;
		AttackDamageTags = ["club", "nevergib"];
	}

	public override bool CanPrimaryAttack() => _strike is null && base.CanPrimaryAttack();

	protected void BeginSwing( string sequence, float damage, float idleAfter )
	{
		if ( _strike is not null ) return;
		_damage = damage;
		_strike = StrikeDelay;
		ShowSwing( sequence, idleAfter, !SwingSoundAtImpact );
	}

	[Rpc.Broadcast( NetFlags.OwnerOnly )]
	void ShowSwing( string sequence, float idleAfter, bool playSound )
	{
		PostWeaponEvent( WeaponEvents.Attack );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( sequence, idleAfter: idleAfter );
		if ( playSound ) PlaySample( Sample( "swing" ) );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( _strike is not { } delay || delay > 0 ) return;
		_strike = null;
		if ( IsProxy || !IsActive || !IsHeld ) return;

		var hit = TraceSwing( AimRay );
		if ( hit.Hit ) ShootBullet( hit, _damage, 0, hitboxTags: false );
		if ( Owner is { ThirdPerson: false } ) MeleeViewPunch.Apply( Scene.Camera, StrikePunch );
		ShootEffects( new ShotEffect( hit.EndPosition, hit.Hit, hit.Normal, hit.GameObject, hit.Surface ) );
	}

	protected override void OnShootEffects( ShotEffect shot )
	{
		if ( Application.IsDedicatedServer ) return;
		if ( SwingSoundAtImpact ) PlaySample( Sample( "swing" ) );
		if ( !shot.Hit ) return;

		var variant = Game.Random.Int( 1, 2 );
		if ( HitFlesh( shot ) )
		{
			PlaySample( Sample( $"hitbody{variant}" ) );
			return;
		}

		PlaySample( Sample( $"hitwall{variant}" ) );
		if ( MarksWalls ) ImpactDecal.Place( shot.HitPosition, shot.Normal, $"shot{Game.Random.Int( 1, 5 )}", 10, ContentMount );
	}

	protected override void OnHolstered()
	{
		_strike = null;
		base.OnHolstered();
	}
}

using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Hide]
public abstract class CounterStrikeShotgun : BulletWeapon
{
	const float ReloadOpeningTime = 0.55f;
	TimeUntil? _firstInsert;
	TimeUntil? _brassDue;

	internal abstract float ShotAttenuation { get; }
	internal abstract int GroundRecoil { get; }
	internal abstract int AirRecoil { get; }
	internal abstract float LoadedIdleDelay { get; }
	internal virtual float EmptyIdleDelay => PrimaryDelay;
	internal virtual float BrassDelay => 0;
	internal virtual string InsertSample => null;
	internal virtual Vector3? BrassOffset => null;

	internal override int PlayerMuzzleFlash => 21;
	internal override string ShotSequence => Game.Random.Int( 1, 2 ) == 1 ? "shoot1" : "shoot2";
	internal override string ReloadSequence => "start_reload";
	internal override float IdleAfterReloadStart => ReloadOpeningTime;
	internal override float? IdleAfterShot => Clip1 > 0 ? LoadedIdleDelay : EmptyIdleDelay;
	internal override (string Name, float? Delay) IdleAnimation => ("idle", 20);

	internal override void Configure()
	{
		HoldType = WeaponHolds.Shotgun;
		DeployTime = 0.75f;
		PrimaryAutomatic = true;
		IncrementalReloading = true;
	}

	public override bool CanSecondaryAttack() => false;
	protected override bool WantsSecondaryAttack() => false;
	public override bool CanReload() => NextPrimaryFire <= 0 && base.CanReload();
	protected override bool WantsPrimaryAttack() => _firstInsert is null && base.WantsPrimaryAttack();

	public override Ray AimRay
	{
		get
		{
			if ( !Owner.IsValid() ) return base.AimRay;
			var eye = Owner.EyeTransform;
			var punch = !IsProxy && !Owner.ThirdPerson ? MeleeViewPunch.Current( Scene.Camera ) : Angles.Zero;
			return new Ray( eye.Position, (eye.Rotation.Angles() + punch).ToRotation().Forward );
		}
	}

	public override void PrimaryAttack()
	{
		if ( IsHeld && !TakePrimaryAmmo() ) return;
		var aim = AimRay;
		var config = Ballistics;
		var volley = new ShotEffect[config.Pellets];
		for ( int i = 0; i < volley.Length; ++i )
		{
			var hit = BulletTrace( aim, config.Range, config.Radius ).Run();
			if ( hit.Hit ) ShootBullet( hit, PelletDamage( hit.Fraction ), config.Force );
			volley[i] = new( hit.EndPosition, hit.Hit, hit.Normal, hit.GameObject, hit.Surface, NoEvents: i > 0 );
		}
		TimeSinceShoot = 0;
		SetNextFire( PrimaryDelay );
		ShootEffects( volley );

		if ( !IsProxy && Owner is { ThirdPerson: false } player )
		{
			var minimum = player.IsOnGround ? GroundRecoil : AirRecoil;
			var recoil = Game.Random.Int( minimum, minimum + (player.IsOnGround ? 2 : 3) );
			var punch = MeleeViewPunch.Current( Scene.Camera );
			MeleeViewPunch.Apply( Scene.Camera, punch + new Angles( -recoil, 0, 0 ) );
		}
	}

	internal int PelletDamage( float fraction ) => (int)((1 - fraction) * Ballistics.Damage);

	internal override Vector2 SamplePelletOffset()
	{
		Vector2 offset;
		do offset = base.SamplePelletOffset();
		while ( offset.LengthSquared > 1 );
		return offset;
	}

	internal override void DrawShotTrail( Vector3 start, Vector3 end ) { }

	internal override void PlayShotSound() =>
		PlaySample( ShotSound, pitch: Game.Random.Int( 94, 109 ) / 100f, attenuation: ShotAttenuation );

	protected override void OnShootEffects( ShotEffect shot )
	{
		base.OnShootEffects( shot );
		if ( shot.NoEvents ) return;
		if ( BrassDelay > 0 ) _brassDue = BrassDelay;
		else EjectShells( 1, true, localOffset: BrassOffset );
	}

	public override void DryFire()
	{
		if ( AutoReload && CanReload() ) Reload();
		PlaySample( "weapons/dryfire_rifle.wav", 0.8f );
		SetNextFire( 1 );
	}

	protected override void OnReloadStarted()
	{
		base.OnReloadStarted();
		_firstInsert = ReloadOpeningTime;
	}

	protected override void OnReloadInserted()
	{
		base.OnReloadInserted();
		if ( Clip1 < ClipMaxSize && Ammo1 > 0 ) InsertShell();
	}

	void InsertShell()
	{
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( "insert" );
		if ( InsertSample is { } sample ) PlaySample( sample, pitch: Game.Random.Int( 85, 116 ) / 100f );
	}

	protected override void OnReloadFinished()
	{
		base.OnReloadFinished();
		_firstInsert = null;
		if ( IsActive && (Clip1 == ClipMaxSize || Ammo1 == 0) )
			ViewModel?.GetComponent<StudioWeaponView>()?.Play( "after_reload", idleAfter: 1.5f );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( _firstInsert is { } first && first <= 0 )
		{
			_firstInsert = null;
			if ( IsReloading ) InsertShell();
		}
		if ( _brassDue is { } brass && brass <= 0 )
		{
			_brassDue = null;
			EjectShells( 1, true, localOffset: BrassOffset );
		}
	}
}

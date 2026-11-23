using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Hide]
public abstract class CounterStrikeSniper : CounterStrikeFirearm
{
	int _scopeLevel;
	int _resumeScopeLevel;
	TimeUntil? _brassDue;
	Texture[] _scopeMasks;

	internal virtual float SecondScopeFov => 15;
	internal virtual float BrassDelay => 0;
	internal virtual string BrassModel => "models/rshell.mdl";
	internal virtual bool CycleBolt => false;
	internal virtual float HipFireSpread => 0.025f;
	internal virtual Angles ShotRecoil => new( -2, 0, 0 );
	internal override float ZoomFieldOfView => _scopeLevel == 2 ? SecondScopeFov : 40;
	internal override string ShotSequence => Game.Random.Int( 0, 1 ) == 0 ? "shoot" : "shoot2";
	internal override string ShotSound => $"weapons/{Definition.Model}-1.wav";
	internal override bool CanIdle => Clip1 > 0;
	internal override float? IdleAfterShot => 1.8f;
	internal override float IdleAfterReloadStart => ReloadTime;
	internal override (string Name, float? Delay) IdleAnimation => ("idle1", 60);

	public override Ray AimRay => Owner.IsValid() ? Owner.EyeTransform.ForwardRay : base.AimRay;

	internal override void Configure()
	{
		HoldType = WeaponHolds.Rifle;
		Handedness = WeaponHandedness.Right;
		PrimaryDelay = 0.25f;
		SecondaryDelay = 0.3f;
		DeployTime = 0.75f;
		AttackDamageTags = ["bullet"];
	}

	internal abstract float CalculateSpread( bool grounded, bool crouched, float speed, bool zoomed );

	public override bool CanSecondaryAttack() => NextSecondaryFire <= 0 && !IsReloading;

	public override void SecondaryAttack()
	{
		SetScope( (_scopeLevel + 1) % 3 );
		PlaySample( "weapons/zoom.wav", 0.2f );
	}

	public override void PrimaryAttack()
	{
		if ( IsHeld && !TakePrimaryAmmo() ) return;
		var player = Owner;
		var speed = player.IsValid() ? player.Velocity.WithZ( 0 ).Length : 0;
		var spread = CalculateSpread( !player.IsValid() || player.IsOnGround, player.IsValid() && player.IsDucking, speed, Zoomed );
		if ( CycleBolt && Zoomed )
		{
			_resumeScopeLevel = _scopeLevel;
			SetScope( 0 );
		}
		SetNextFire( PrimaryDelay );
		FireRound( spread );

		if ( !IsProxy && Owner is { ThirdPerson: false } )
			MeleeViewPunch.Apply( Scene.Camera, ShotRecoil );
	}

	public override void DryFire()
	{
		PlaySample( "weapons/dryfire_rifle.wav" );
		SetNextFire( 0.2f );
	}

	protected override void OnShootEffects( ShotEffect shot )
	{
		base.OnShootEffects( shot );
		if ( shot.NoEvents ) return;
		if ( BrassDelay > 0 ) _brassDue = BrassDelay;
		else EjectShells( 1, false, BrassModel );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( IsActive && _resumeScopeLevel != 0 && NextPrimaryFire <= 0 )
		{
			SetScope( _resumeScopeLevel );
			_resumeScopeLevel = 0;
		}
		if ( _brassDue is { } due && due <= 0 )
		{
			_brassDue = null;
			EjectShells( 1, false, BrassModel );
		}
	}

	void SetScope( int level )
	{
		_scopeLevel = level;
		Zoomed = level != 0;
		ApplyScopeToViewModel();
	}

	void ResetScope()
	{
		_resumeScopeLevel = 0;
		SetScope( 0 );
	}

	void ApplyScopeToViewModel()
	{
		if ( ViewModel?.GetComponent<SkinnedModelRenderer>() is { } renderer )
			renderer.RenderOptions.Overlay = !Zoomed;
	}

	protected override void CreateViewModel()
	{
		base.CreateViewModel();
		ApplyScopeToViewModel();
	}

	protected override void OnHolstered()
	{
		ResetScope();
		base.OnHolstered();
	}

	protected override void OnReloadStarted()
	{
		ResetScope();
		base.OnReloadStarted();
	}

	public override void DrawCrosshair( Painter painter, Vector2 center )
	{
		if ( Zoomed && CrosshairVisible() && ContentMount is { } mount )
		{
			_scopeMasks ??= SniperScope.Load( mount );
			if ( _scopeMasks.All( mask => mask is { IsError: false } ) )
				SniperScope.Draw( painter, _scopeMasks );
		}
		base.DrawCrosshair( painter, center );
	}
}

[Hide]
public abstract class BoltActionSniper : CounterStrikeSniper
{
	internal override bool CycleBolt => true;
	internal override string BrassModel => "models/rshell_big.mdl";
}

[Hide]
public abstract class SemiAutomaticSniper : CounterStrikeSniper
{
	float? _lastShot;
	protected float Accuracy { get; set; }

	internal abstract float RecoveryRate { get; }
	internal abstract float RapidFireAccuracy { get; }
	internal abstract float StartingAccuracy { get; }

	protected void RecoverAccuracy( float firstShotAccuracy )
	{
		Accuracy = _lastShot is { } time
			? System.MathF.Min( 0.98f, (Time.Now - time) * RecoveryRate + RapidFireAccuracy )
			: firstShotAccuracy;
		_lastShot = Time.Now;
	}

	protected override void OnEquipped()
	{
		Accuracy = StartingAccuracy;
		base.OnEquipped();
	}
}

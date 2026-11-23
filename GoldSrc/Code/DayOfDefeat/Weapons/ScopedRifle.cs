using System;
using GoldSrc.HalfLife;
using Sandbox.Rendering;

namespace GoldSrc.DayOfDefeat;

[Hide]
public abstract class ScopedRifle : DayOfDefeatFirearm
{
	TimeUntil? _scopeChange;
	bool _resumeScope;
	Texture[] _scopeMasks;

	internal abstract string ScopeSprite { get; }
	internal virtual float ScopedSpread => 0.004f;
	internal virtual float ScopeReturnDelay => 1.1f;
	internal virtual bool CyclesBolt => true;
	internal override float ZoomFieldOfView => 20;
	internal override float StandingSpread => 0.06f;
	internal override int PlayerMuzzleFlash => 41;
	internal override int BrassBodyGroup => 2;
	internal override Vector2 PelletSpread => Zoomed
		? Vector2.One * (ScopedSpread + (Owner is { } player && player.Velocity.Length > 45 ? MovingSpreadPenalty : 0))
		: base.PelletSpread;
	internal override float? IdleAfterShot => PrimaryDelay;

	internal override void Configure()
	{
		base.Configure();
		HoldType = WeaponHolds.Rifle;
		ClipMaxSize = 5;
		SecondaryDelay = 1.2f;
		Ballistics = Ballistics with { Damage = 160 };
	}

	public override bool CanSecondaryAttack() => IsActive && NextSecondaryFire <= 0 && !IsReloading;
	public override bool CanReload() => NextPrimaryFire <= 0 && base.CanReload();

	public override void SecondaryAttack()
	{
		_scopeChange = null;
		SetScope( !Zoomed );
		SetNextPrimaryFire( 0.5f );
	}

	public override void PrimaryAttack()
	{
		if ( Owner is { } player && WaterBody.Find( Scene, player.EyePosition ).IsValid() )
		{
			base.PrimaryAttack();
			return;
		}

		SetNextFire( PrimaryDelay );
		base.PrimaryAttack();
		if ( Zoomed && (CyclesBolt || Clip1 == 0) )
		{
			_resumeScope = CyclesBolt && Clip1 > 0;
			_scopeChange = 0.5f;
		}
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( IsProxy || !IsActive || _scopeChange is not { } change || change > 0 ) return;

		_scopeChange = null;
		if ( Zoomed )
		{
			SetScope( false );
			if ( _resumeScope ) _scopeChange = ScopeReturnDelay;
		}
		else if ( _resumeScope && Owner is { IsOnGround: true } player && player.Velocity.Length <= 45 )
		{
			SetScope( true );
		}
	}

	// Apply the same sway to the view and the shot so they stay aligned through the scope.
	Rotation ScopedRotation( PlayerController player )
	{
		var time = Time.Now;
		var sway = player.IsDucking ? 0.1f : 0.45f;
		var drift = new Angles( MathF.Sin( time * 2.3f ) * sway, MathF.Sin( time * 1.7f ) * sway, 0 );
		return player.EyeTransform.Rotation * Rotation.From( drift );
	}

	public override Ray AimRay => Zoomed && Owner is { } player
		? new Ray( player.EyePosition, ScopedRotation( player ).Forward )
		: base.AimRay;

	protected override void ModifyCamera( CameraComponent camera, ref CameraView view )
	{
		base.ModifyCamera( camera, ref view );
		if ( Zoomed && Owner is { } player )
		{
			view.Position = player.EyePosition;
			view.Rotation = ScopedRotation( player );
		}
	}

	void SetScope( bool scoped )
	{
		Zoomed = scoped;
		if ( ViewModel?.GetComponent<SkinnedModelRenderer>() is { } renderer )
			renderer.RenderOptions.Overlay = !scoped;
	}

	protected override void CreateViewModel()
	{
		base.CreateViewModel();
		SetScope( Zoomed );
	}

	protected override void OnReloadStarted()
	{
		_scopeChange = null;
		SetScope( false );
		base.OnReloadStarted();
	}

	protected override void OnHolstered()
	{
		_scopeChange = null;
		SetScope( false );
		base.OnHolstered();
	}

	public override void DrawCrosshair( Painter painter, Vector2 center )
	{
		if ( !Zoomed || !CrosshairVisible() ) return;
		_scopeMasks ??= ContentMount?.GetIndexedSprite( ScopeSprite )?.Animations[0].Frames.Select( frame => frame.Texture ).ToArray();
		if ( _scopeMasks is { Length: 4 } ) ScopeOverlay.Draw( painter, _scopeMasks );
	}
}

using GoldSrc.HalfLife;
using Sandbox.Rendering;

namespace GoldSrc.HalfLifeMMod;

[Title( "Crossbow" ), Category( "Half-Life: MMod" )]
[WeaponPickup( "crossbow", "bolts", 50, Bucket = 2, Weight = 10, Mount = "hl1mmod" )]
public sealed class Crossbow : ProjectileWeapon
{
	bool _hasDeployed;
	int _displayedClip = -1;
	Sprite _scope;
	SoundHandle _breathing;

	internal override float ZoomFieldOfView => 20;
	internal override string DeploySequence => !_hasDeployed ? "draw_first" : Clip1 == 0 ? "draw2" : "draw1";
	internal override string ShotSequence => Clip1 > 0 ? "fire1" : "fire3";
	internal override float? IdleAfterShot => Clip1 > 0 ? 5 : 1.25f;
	internal override string ReloadSequence => Clip1 == 0 ? "reload_empty" : "reload";
	internal override float IdleAfterReloadStart => Clip1 == 0 ? 4.53f : 2.37f;
	internal override Vector3 WorldMuzzleOffset( bool hd ) => new( 19.3f, 0.8f, 2.6f );
	public override Ray AimRay => Owner.IsValid() ? OffsetLaunchRay( Zoomed ? new Vector3( 0, 0, -2 ) : new Vector3( 0, -3, -5 ) ) : base.AimRay;

	internal override void Configure()
	{
		HoldType = WeaponHolds.Rifle;
		ClipMaxSize = 5;
		PrimaryDelay = 1.25f;
		SecondaryDelay = 1;
		ReloadTime = 2.37f;
	}

	protected override void OnEquipped()
	{
		DeployTime = _hasDeployed ? 0.5f : 3.47f;
		base.OnEquipped();
		_hasDeployed = true;
	}

	internal override void PlayIdle( StudioWeaponView view )
	{
		if ( Zoomed )
		{
			view.Play( "scope_idle", idleAfter: 0.2f );
			return;
		}

		var fidget = Game.Random.Float( 0, 1 ) > 0.75f;
		view.Play( fidget ? (Clip1 > 0 ? "fidget1" : "fidget2") : (Clip1 > 0 ? "idle1" : "idle2") );
	}

	public override void PrimaryAttack()
	{
		SetNextSecondaryFire( PrimaryDelay );
		base.PrimaryAttack();
	}

	internal override bool Release( Ray ray )
	{
		var speed = WaterBody.Find( Scene, Owner.EyePosition ).IsValid() ? 1500 : 2500;
		GoldSrc.HalfLife.Npc.PunchPlayer( Owner, Vector3.Zero, new Angles( -2, 0, 0 ) );
		NpcProjectile.Fire<MModCrossbowBolt>( Owner.GameObject, "mount://hl1mmod/valve/models/crossbow_bolt.mdl.vmdl",
			50, null, ray.Position, ray.Forward, speed );
		return true;
	}

	internal override void PlayLaunchSound()
	{
		PlaySample( "weapons/xbow_fire1.wav", pitch: Game.Random.Int( 93, 108 ) / 100f );
		ApplyArrows();
	}

	public override void SecondaryAttack()
	{
		base.SecondaryAttack();
		PlaySample( Zoomed ? "weapons/xbow_zoomin.wav" : "weapons/xbow_zoomout.wav" );
		if ( Zoomed )
		{
			_breathing = PlaySample( "player/breathe1.wav" );
			ViewModel?.GetComponent<StudioWeaponView>()?.Play( "scope_idle", idleAfter: 0.2f );
		}
		else
		{
			StopBreathing();
			ViewModel?.GetComponent<StudioWeaponView>()?.Play( "scope_out" );
		}
	}

	public override void DrawCrosshair( Painter painter, Vector2 center )
	{
		if ( !Zoomed )
		{
			base.DrawCrosshair( painter, center );
			return;
		}
		if ( !CrosshairVisible() ) return;

		_scope ??= ContentMount?.GetIndexedSprite( "hud_scope" );
		if ( _scope?.Animations[0].Frames is not { Count: >= 6 } frames ) return;
		var half = painter.Bounds.Height * 0.5f;
		var left = center.x - half;
		var top = center.y - half;
		System.Span<Rect> rectangles = stackalloc Rect[]
		{
			new( left, top, half, half ),
			new( center.x, top, half, half ),
			new( center.x, center.y, half, half ),
			new( left, center.y, half, half ),
			new( painter.Bounds.Left, top, System.MathF.Max( 0, left - painter.Bounds.Left ), half * 2 ),
			new( center.x + half, top, System.MathF.Max( 0, painter.Bounds.Right - center.x - half ), half * 2 )
		};
		for ( int i = 0; i < rectangles.Length; i++ )
			if ( rectangles[i].Width > 0 ) painter.Texture( frames[i].Texture, rectangles[i], Color.White, FilterMode.Bilinear );
	}

	public override void Reload()
	{
		if ( Networking.IsHost && CanReload() ) ReloadTime = IdleAfterReloadStart;
		base.Reload();
	}

	protected override void OnReloadStarted()
	{
		if ( Zoomed ) PlaySample( "weapons/xbow_zoomout.wav" );
		StopBreathing();
		base.OnReloadStarted();
		PlaySample( "weapons/xbow_reload1.wav", pitch: Game.Random.Int( 93, 108 ) / 100f );
	}

	protected override void OnReloadFinished()
	{
		base.OnReloadFinished();
		ApplyArrows();
	}

	protected override void OnHolstered()
	{
		StopBreathing();
		base.OnHolstered();
	}

	protected override void CreateViewModel()
	{
		base.CreateViewModel();
		ApplyArrows();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( !IsReloading && _displayedClip != Clip1 ) ApplyArrows();
	}

	protected override void OnDestroy()
	{
		StopBreathing();
		base.OnDestroy();
	}

	void StopBreathing()
	{
		_breathing?.Stop();
		_breathing = null;
	}

	void ApplyArrows()
	{
		if ( ViewModel?.GetComponent<SkinnedModelRenderer>() is not { } renderer ) return;
		renderer.SetBodyGroup( "arrows", System.Math.Clamp( 4 - Clip1, 0, 4 ) );
		_displayedClip = Clip1;
	}

	internal override void OnViewSequenceEvent( Mdl.Event animationEvent, SkinnedModelRenderer renderer )
	{
		switch ( animationEvent.Index )
		{
			case 5008 when int.TryParse( animationEvent.Options, out var arrows ):
				renderer.SetBodyGroup( "arrows", arrows );
				break;
			case 5009:
				PlaySample( animationEvent.Options );
				break;
		}
	}
}

[Hide]
public sealed class MModCrossbowBolt : CrossbowBolt
{
	internal override bool Explosive => false;
	internal override string ImpactMount => "hl1mmod";
}

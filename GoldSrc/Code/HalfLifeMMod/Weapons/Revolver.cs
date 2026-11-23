using GoldSrc.HalfLife;

namespace GoldSrc.HalfLifeMMod;

[Title( ".357 Magnum" ), Category( "Half-Life: MMod" )]
[WeaponPickup( "357", "357", 36, Bucket = 1, Weight = 15, Mount = "hl1mmod" )]
public sealed class Revolver : BulletWeapon
{
	const float MinimumSpread = 0.00873f;
	const float AttachmentDuration = 1.8f;
	const float AttachmentFinishDuration = 1.37f;

	[Sync] public bool LaserAttached { get; private set; }
	[Sync] public bool LaserEnabled { get; private set; }

	TimeUntil? _attachmentTime;
	bool _installing;
	bool _hasDeployed;
	float _spread = MinimumSpread;
	SpriteRenderer _dot;

	internal override int PlayerMuzzleFlash => 31;
	internal override Vector2 PelletSpread => Vector2.One * _spread;
	internal override float ZoomFieldOfView => 40;
	internal override string DeploySequence => _hasDeployed ? "draw" : "deploy_first";
	internal override string ShotSequence => LaserAttached ? "fire1" : "quickfire1";
	internal override float IdleAfterReloadStart => 3.1f;
	internal override Vector3 WorldMuzzleOffset( bool hd ) => new( 13.8f, 1.6f, 3.3f );
	internal override (string Name, float? Delay) IdleAnimation => Game.Random.Float( 0, 1 ) switch
	{
		<= 0.5f => ("idle1", 70f / 30),
		<= 0.7f => ("idle2", 2),
		<= 0.9f => ("idle3", 88f / 30),
		_ => ("fidget1", 170f / 30)
	};

	internal override void Configure()
	{
		HoldType = WeaponHolds.Pistol;
		ClipMaxSize = 6;
		PrimaryDelay = 0.45f;
		SecondaryDelay = 0.5f;
		ReloadTime = 3;
		AttackDamageTags = ["bullet"];
		Ballistics = new() { Damage = 40, Range = 8192, Radius = 0 };
	}

	protected override void OnEquipped()
	{
		DeployTime = _hasDeployed ? 0.5f : 2.43f;
		_spread = MinimumSpread;
		base.OnEquipped();
		_hasDeployed = true;
	}

	public override bool CanPrimaryAttack() => _attachmentTime is null && base.CanPrimaryAttack();
	public override bool CanSecondaryAttack() => LaserAttached && _attachmentTime is null && base.CanSecondaryAttack();
	public override bool CanReload() => _attachmentTime is null && base.CanReload();

	public override void PrimaryAttack()
	{
		if ( Underwater() )
		{
			DryFire();
			return;
		}

		_spread = LaserEnabled ? 0 : LaserAttached
			? System.MathF.Min( 0.02618f, _spread + 0.003f )
			: System.MathF.Min( 0.45f, _spread + 0.025f );
		SetNextPrimaryFire( LaserAttached ? 0.75f : PrimaryDelay );
		base.PrimaryAttack();
	}

	bool Underwater() => Owner.IsValid() && WaterBody.Find( Scene, Owner.EyePosition ).IsValid();

	public override void DryFire()
	{
		if ( _attachmentTime is not null ) return;
		PlaySample( "weapons/weapon_empty_357.wav" );
		SetNextPrimaryFire( 0.15f );
	}

	internal override void DrawShotTrail( Vector3 start, Vector3 end ) { }

	internal override void PlayShotSound() =>
		PlaySample( $"weapons/357_shot{Game.Random.Int( 1, 2 )}.wav", Game.Random.Float( 0.8f, 0.9f ) );

	protected override void OnShootEffects( ShotEffect shot )
	{
		base.OnShootEffects( shot );
		if ( shot.NoEvents || IsProxy || Owner is not { ThirdPerson: false } ) return;
		MeleeViewPunch.Apply( Scene.Camera, new Angles( -10, 0, 0 ) );
		if ( ViewModel?.GetComponent<SkinnedModelRenderer>() is { } renderer )
			StudioSequenceEvents.ShowMuzzleFlash( ContentMount, renderer, GetMuzzleTransform(), 4 );
	}

	public override void SecondaryAttack()
	{
		if ( !LaserAttached || _attachmentTime is not null ) return;
		base.SecondaryAttack();
		LaserEnabled = Zoomed;
		ApplyLaserBodyGroup();
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( LaserEnabled ? "add_on" : "add_off", idleAfter: 0.77f );
		PlayLaserSwitch( LaserEnabled );
	}

	[Rpc.Broadcast( NetFlags.OwnerOnly )]
	void PlayLaserSwitch( bool enabled ) =>
		PlaySample( enabled ? "weapons/rpg_spot_on.wav" : "weapons/rpg_spot_off.wav" );

	protected override void Think()
	{
		base.Think();
		if ( !Input.Down( "attack1" ) )
			_spread = System.MathF.Max( MinimumSpread, _spread - 0.001f );
		if ( Input.Pressed( "ToolMenu" ) && _attachmentTime is null && !IsReloading && NextPrimaryFire <= 0 )
			BeginAttachmentChange();
		UpdateAttachment();
	}

	void BeginAttachmentChange()
	{
		_installing = !LaserAttached;
		_attachmentTime = AttachmentDuration;
		SetNextFire( AttachmentDuration );
		if ( LaserEnabled )
		{
			base.SecondaryAttack();
			LaserEnabled = false;
			PlayLaserSwitch( false );
		}
		ApplyLaserBodyGroup();
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( _installing ? "add_laser1" : "remove_laser",
			idleAfter: _installing ? AttachmentDuration - AttachmentFinishDuration : AttachmentDuration );
	}

	void UpdateAttachment()
	{
		if ( _attachmentTime is not { } remaining ) return;
		if ( _installing && !LaserAttached && remaining <= AttachmentFinishDuration )
		{
			LaserAttached = true;
			ApplyLaserBodyGroup();
			ViewModel?.GetComponent<StudioWeaponView>()?.Play( "add_laser2", idleAfter: remaining );
		}
		if ( remaining > 0 ) return;
		_attachmentTime = null;
		LaserAttached = _installing;
		ApplyLaserBodyGroup();
	}

	protected override void CreateViewModel()
	{
		base.CreateViewModel();
		ApplyLaserBodyGroup();
	}

	void ApplyLaserBodyGroup() =>
		ViewModel?.GetComponent<SkinnedModelRenderer>()?.SetBodyGroup( "scope", LaserEnabled ? 2 : LaserAttached ? 1 : 0 );

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( Application.IsDedicatedServer ) return;
		if ( !IsActive || !Owner.IsValid() || !LaserEnabled || IsReloading )
		{
			if ( _dot.IsValid() ) _dot.Enabled = false;
			return;
		}

		var sight = Owner.EyeTransform.ForwardRay;
		var hit = Scene.Trace.Ray( sight, Ballistics.Range ).IgnoreGameObjectHierarchy( Owner.GameObject ).UseHitboxes().Run();
		if ( !_dot.IsValid() ) _dot = LaserSpot.Create( GameObject, ContentMount, "Python laser spot" );
		if ( !_dot.IsValid() ) return;
		_dot.Enabled = hit.Hit;
		_dot.WorldPosition = hit.EndPosition - sight.Forward * 0.5f;
		var image = _dot.Sprite.Animations[0].Frames[0].Texture;
		var scale = hit.Distance / 3800f;
		_dot.Size = new Vector2( image.Width, image.Height ) * scale;
	}

	protected override void OnReloadStarted()
	{
		ClearLaser();
		_spread = MinimumSpread;
		base.OnReloadStarted();
	}

	protected override void OnHolstered()
	{
		ClearLaser();
		_attachmentTime = null;
		base.OnHolstered();
	}

	void ClearLaser()
	{
		if ( !IsProxy && LaserEnabled )
		{
			LaserEnabled = false;
			PlayLaserSwitch( false );
		}
		ApplyLaserBodyGroup();
	}

	internal override void OnViewSequenceEvent( Mdl.Event animationEvent, SkinnedModelRenderer renderer )
	{
		if ( animationEvent.Index != 7002 || animationEvent.Options != "1" || !Owner.IsValid() ) return;
		var chamber = renderer.GetAttachment( "attachment2" ) ?? GetMuzzleTransform();
		var tumble = Vector3.Random * 15;
		ShellCasing.Eject( chamber.Position, chamber.Rotation, false, Owner.Velocity + tumble, ContentMount, "models/shell_python.mdl" );
	}
}

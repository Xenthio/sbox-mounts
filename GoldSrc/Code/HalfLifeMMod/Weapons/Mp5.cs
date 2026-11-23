using GoldSrc.HalfLife;

namespace GoldSrc.HalfLifeMMod;

[Title( "MP5" ), Category( "Half-Life: MMod" )]
[WeaponPickup( "9mmar", "9mm", 250, Bucket = 2, Weight = 15, Mount = "hl1mmod" )]
public sealed class Mp5 : BulletWeapon
{
	const float MinimumSpread = 0.01745f;
	const float SpreadStep = 0.003f;
	const float AttachmentTime = 3.44f;
	const float AttachmentLeadIn = AttachmentTime - 2.83f;
	const string GrenadeModel = "mount://hl1mmod/HL1MMod/models/grenade.mdl.vmdl";

	[Sync] public bool Suppressed { get; private set; }
	TimeUntil _nextLaunch;
	TimeUntil? _attachmentTime;
	bool _installing;
	bool _hasDeployed;
	bool _tracer;
	float _spread = MinimumSpread;

	internal override int PlayerMuzzleFlash => Suppressed ? -1 : 40;
	internal override Vector2 PelletSpread => Vector2.One * _spread;
	internal override string DeploySequence => _hasDeployed ? "deploy_1" : "deploy_first";
	internal override (string Name, float? Delay) IdleAnimation =>
		(Game.Random.Int( 0, 1 ) == 0 ? "longidle" : "idle1", Game.Random.Float( 10, 15 ));
	internal override string ShotSequence => $"shoot_{(Suppressed ? "silenced_" : "")}{Game.Random.Int( 1, 3 )}";
	internal override string ReloadSequence => Clip1 == 0 ? "reload2" : "reload";
	internal override float IdleAfterReloadStart => Clip1 == 0 ? 2.03f : 1.57f;
	internal override Vector3 WorldMuzzleOffset( bool hd ) => new( 17.8f, 1.65f, 4.9f );

	internal override void Configure()
	{
		HoldType = WeaponHolds.Smg;
		ClipMaxSize = 50;
		PrimaryDelay = 0.1f;
		SecondaryDelay = 1.97f;
		SecondaryAutomatic = true;
		ReloadTime = 1.57f;
		AttackDamageTags = ["bullet"];
		Ballistics = new() { Damage = 5, Range = 8192, Radius = 0 };
	}

	internal override void BindAmmoTypes()
	{
		base.BindAmmoTypes();
		SecondaryAmmoType ??= GameResource.Load<BaseAmmoResource>( $"mount://hl1mmod/{Rifle.GrenadeAmmoPath}" );
	}

	protected override bool OnAdding( BaseInventoryComponent inventory )
	{
		if ( Clip1 < 0 ) Clip1 = 25;
		return base.OnAdding( inventory );
	}

	protected override void OnEquipped()
	{
		DeployTime = _hasDeployed ? 0.5f : 1.7f;
		_spread = MinimumSpread;
		base.OnEquipped();
		_hasDeployed = true;
	}

	public override bool CanPrimaryAttack() => _attachmentTime is null && base.CanPrimaryAttack();
	public override bool CanSecondaryAttack() => _attachmentTime is null && base.CanSecondaryAttack();
	public override bool CanReload() => _attachmentTime is null && base.CanReload();

	public override void Reload()
	{
		if ( Networking.IsHost && CanReload() ) ReloadTime = IdleAfterReloadStart;
		base.Reload();
	}

	protected override void OnReloadStarted()
	{
		_spread = MinimumSpread;
		base.OnReloadStarted();
	}

	public override void PrimaryAttack()
	{
		if ( Underwater() )
		{
			DryFire();
			return;
		}
		_spread = System.MathF.Min( 0.05234f, _spread + SpreadStep );
		base.PrimaryAttack();
	}

	public override void DryFire()
	{
		if ( _attachmentTime is not null ) return;
		PlaySample( "weapons/weapon_empty_mp5.wav" );
		SetNextFire( 0.15f );
	}

	bool Underwater() => Owner.IsValid() && WaterBody.Find( Scene, Owner.EyePosition ).IsValid();

	protected override void OnShootEffects( ShotEffect shot )
	{
		_tracer = !_tracer;
		base.OnShootEffects( shot );
		if ( shot.NoEvents ) return;
		if ( !IsProxy && Owner is { ThirdPerson: false } )
		{
			MeleeViewPunch.Apply( Scene.Camera, new Angles( Game.Random.Float( -2, 2 ), 0, 0 ) );
			if ( !Suppressed && ViewModel?.GetComponent<SkinnedModelRenderer>() is { } renderer )
				StudioSequenceEvents.ShowMuzzleFlash( ContentMount, renderer, GetMuzzleTransform(), 2 );
		}
		EjectShells( 1, false );
	}

	internal override void PlayShotSound() =>
		PlaySample( $"weapons/hks{Game.Random.Int( 1, 3 )}{(Suppressed ? "_sil" : "")}.wav" );

	internal override void DrawShotTrail( Vector3 start, Vector3 end )
	{
		if ( _tracer ) base.DrawShotTrail( start, end );
	}

	public override void SecondaryAttack()
	{
		if ( Underwater() )
		{
			DryFire();
			return;
		}
		SetNextFire( LaunchDelay( Ammo2 <= 1 ) );
		LaunchGrenade( AimRay );
	}

	static float LaunchDelay( bool last ) => last ? 1.1f : 1.97f;

	[Rpc.Host( NetFlags.OwnerOnly )]
	void LaunchGrenade( Ray aim )
	{
		if ( !IsActive || !Owner.IsValid() || _nextLaunch > 0 || IsReloading || Underwater() ) return;
		if ( !TakeSecondaryAmmo() ) return;
		bool last = Ammo2 == 0;
		_nextLaunch = LaunchDelay( last );
		NpcProjectile.Fire<ContactGrenade>( Owner.GameObject, GrenadeModel, 100, null, aim.Position, aim.Forward, 800 );
		LaunchEffects( last );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void LaunchEffects( bool last )
	{
		ShowPlayerMuzzleFlash();
		if ( !IsProxy && Owner is { ThirdPerson: false } )
			MeleeViewPunch.Apply( Scene.Camera, new Angles( -10, 0, 0 ) );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( last ? "grenade_last" : "grenade", idleAfter: 3 );
		PlaySample( Game.Random.Int( 0, 1 ) == 0 ? "weapons/glauncher.wav" : "weapons/glauncher2.wav" );
		PostWeaponEvent( WeaponEvents.Attack );
	}

	protected override void Think()
	{
		base.Think();
		if ( !Input.Down( "attack1" ) )
			_spread = System.MathF.Max( MinimumSpread, _spread - SpreadStep );
		if ( Input.Pressed( "ToolMenu" ) && _attachmentTime is null && !IsReloading && NextPrimaryFire <= 0 )
			BeginAttachmentChange();
		UpdateAttachment();
	}

	void BeginAttachmentChange()
	{
		_attachmentTime = AttachmentTime;
		_installing = !Suppressed;
		SetNextFire( AttachmentTime );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( Suppressed ? "remove_silencer" : "add_silencer1",
			idleAfter: Suppressed ? AttachmentTime : AttachmentLeadIn );
	}

	void UpdateAttachment()
	{
		if ( _attachmentTime is not { } remaining ) return;
		if ( _installing && !Suppressed && remaining <= 2.83f )
		{
			Suppressed = true;
			ApplySuppressor();
			ViewModel?.GetComponent<StudioWeaponView>()?.Play( "add_silencer2", idleAfter: remaining );
		}
		if ( remaining > 0 ) return;
		_attachmentTime = null;
		Suppressed = _installing;
		ApplySuppressor();
	}

	protected override void CreateViewModel()
	{
		base.CreateViewModel();
		ApplySuppressor();
	}

	void ApplySuppressor() =>
		ViewModel?.GetComponent<SkinnedModelRenderer>()?.SetBodyGroup( "silencer", Suppressed ? 1 : 0 );

	protected override void OnHolstered()
	{
		_attachmentTime = null;
		base.OnHolstered();
	}
}

using GoldSrc.HalfLife;

namespace GoldSrc.HalfLifeMMod;

[Title( "Glock" ), Category( "Half-Life: MMod" )]
[WeaponPickup( "9mmhandgun", "9mm", 250, Bucket = 1, Weight = 10, Mount = "hl1mmod" )]
public sealed class Glock : BulletWeapon
{
	[Sync] public bool Suppressed { get; private set; }
	TimeUntil? _attachment;
	bool _rapid;

	internal override int PlayerMuzzleFlash => Suppressed ? -1 : 21;
	internal override Vector2 PelletSpread => Vector2.One * (_rapid ? 0.1f : 0.01f);
	internal override string ShotSequence => (Suppressed, Clip1 == 0) switch
	{
		(true, true) => "shoot_silenced_empty",
		(true, false) => "shoot_silenced",
		(false, true) => "shoot_empty",
		_ => "shoot"
	};
	internal override bool CanIdle => Clip1 > 0;
	internal override string ReloadSequence => Clip1 > 0 ? "reload_noshot" : "reload";
	internal override float IdleAfterReloadStart => Clip1 > 0 ? 2.28f : 2.75f;

	internal override void Configure()
	{
		HoldType = WeaponHolds.Pistol;
		Handedness = WeaponHandedness.Right;
		PrimaryAutomatic = false;
		SecondaryAutomatic = true;
		PrimaryDelay = 0.3f;
		SecondaryDelay = 0.2f;
		ReloadTime = 2.75f;
		ClipMaxSize = 17;
		AttackDamageTags = ["bullet"];
		Ballistics = new() { Damage = 8, Range = 8192, Radius = 0 };
	}

	public override bool HasSecondaryAmmo() => HasPrimaryAmmo();
	public override bool CanPrimaryAttack() => _attachment is null && base.CanPrimaryAttack();
	public override bool CanSecondaryAttack() => _attachment is null && base.CanSecondaryAttack();
	public override bool CanReload() => _attachment is null && base.CanReload();
	public override void PrimaryAttack() => Fire( false );
	public override void SecondaryAttack() => Fire( true );

	void Fire( bool rapid )
	{
		_rapid = rapid;
		base.PrimaryAttack();
		SetNextFire( rapid ? SecondaryDelay : PrimaryDelay );
	}

	protected override void Think()
	{
		base.Think();
		if ( Input.Pressed( "ToolMenu" ) && _attachment is null && !IsReloading && NextPrimaryFire <= 0 )
			BeginAttachmentChange();
		FinishAttachmentChange();
	}

	void BeginAttachmentChange()
	{
		const float Duration = 2.78f;
		_attachment = Duration;
		SetNextFire( Duration );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( Suppressed ? "remove_silencer" : "add_silencer2" );
	}

	void FinishAttachmentChange()
	{
		if ( _attachment is not { } time || time > 0 ) return;
		_attachment = null;
		Suppressed = !Suppressed;
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
		_attachment = null;
		base.OnHolstered();
	}

	public override void Reload()
	{
		if ( Networking.IsHost ) ReloadTime = IdleAfterReloadStart;
		base.Reload();
	}

	public override void DryFire()
	{
		if ( _attachment is not null ) return;
		PlaySample( "weapons/weapon_empty_glock.wav" );
		SetNextFire( 0.2f );
	}

	internal override void PlayShotSound() =>
		PlaySample( Suppressed ? "weapons/pl_gun1.wav" : $"weapons/pl_gun{Game.Random.Int( 2, 3 )}.wav" );

	internal override void DrawShotTrail( Vector3 start, Vector3 end ) { }

	protected override void OnShootEffects( ShotEffect shot )
	{
		base.OnShootEffects( shot );
		if ( !shot.NoEvents ) EjectShells( 1, false );
	}
}

using GoldSrc.HalfLife;

namespace GoldSrc.TeamFortressClassic;

[Title( "Sniper Rifle" ), Category( "Team Fortress Classic" )]
[WeaponPickup( "tfc_sniper", "buckshot", 200, EntityName = "tf_weapon_sniperrifle", PickupModel = "p_sniper", Bucket = 1, Weight = 10, Mount = "tfc" )]
public sealed class SniperRifle : BulletWeapon
{
	const float MinimumDamage = 50;
	const float MaximumDamage = 400;
	const float ChargePerSecond = 50;
	const float AimSpeed = 80;
	const float StartSpeed = 50;

	[Sync( SyncFlags.FromHost )] bool Charging { get; set; }
	[Sync( SyncFlags.FromHost )] TimeSince ChargeTime { get; set; }
	bool _triggerHeld;
	bool _clicked;
	TimeUntil _ready;
	SpriteRenderer _spot;
	PlayerController _aimingPlayer;
	StudioWeaponView View => ViewModel?.GetComponent<StudioWeaponView>();
	float ChargeDamage => System.MathF.Min( MinimumDamage + ChargeTime * ChargePerSecond, MaximumDamage );

	internal override float ZoomFieldOfView => 20;
	internal override string WorldModelPath => "models/p_sniper.mdl";
	internal override string ShotSequence => "fire";
	internal override string ShotSound => "ambience/rifle1.wav";
	internal override float? IdleAfterShot => 0.5f;
	internal override (string Name, float? Delay) IdleAnimation => ("idle", 12.5f);
	internal override int PlayerMuzzleFlash => 20;
	internal override Vector2 PelletSpread => Vector2.Zero;

	internal override void Configure()
	{
		HoldType = WeaponHolds.Rifle;
		UsesClips = false;
		StartingAmmo = 5;
		PrimaryDelay = 1.5f;
		SecondaryDelay = 0.3f;
		AttackDamageTags = ["bullet"];
		Ballistics = new() { Damage = MinimumDamage, Range = 8192, Radius = 0 };
	}

	public override Ray AimRay => Owner.IsValid() ? OffsetLaunchRay( Vector3.Left * 2 ) : base.AimRay;

	public override bool CanPrimaryAttack() => !_triggerHeld && !Charging &&
		Owner is { } player && player.Velocity.WithZ( 0 ).Length <= StartSpeed && base.CanPrimaryAttack();

	public override void PrimaryAttack()
	{
		if ( _triggerHeld || !Owner.IsValid() ) return;
		_triggerHeld = true;
		SetNextPrimaryFire( 0 );
		BeginCharge();
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void BeginCharge()
	{
		if ( !IsActive || Charging || _ready > 0 || !HasPrimaryAmmo() ||
			Owner is not { } player || player.Velocity.WithZ( 0 ).Length > StartSpeed ) return;
		ChargeTime = 0;
		Charging = true;
		ShowAim( true );
	}

	protected override void Think()
	{
		base.Think();
		if ( Input.Down( "attack1" ) ) return;
		_clicked = false;
		if ( !_triggerHeld ) return;
		_triggerHeld = false;
		ReleaseCharge();
	}

	public override void DryFire()
	{
		if ( _clicked ) return;
		_clicked = true;
		PlaySample( "weapons/357_cock1.wav", 0.8f );
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void ReleaseCharge()
	{
		if ( !Charging ) return;
		var damage = ChargeDamage;
		Charging = false;
		ShowAim( false );
		if ( !IsActive || Owner is not { IsOnGround: true } || !TakePrimaryAmmo() ) return;

		_ready = PrimaryDelay;
		var ray = AimRay;
		var hit = TraceLineOfFire( ray, Ballistics.Range, 0 ).UseHitboxes().Run();
		if ( hit.Hit )
		{
			if ( hit.GameObject?.GetComponentInParent<PlayerController>() is not null )
				damage *= PlayerHitMultiplier( hit.Hitbox?.Tags );
			ShootBullet( hit, damage, 0 );
		}
		ShowFire( new ShotEffect( hit.EndPosition, hit.Hit, hit.Normal, hit.GameObject, hit.Surface, Origin: ray.Position ) );
	}

	internal static float PlayerHitMultiplier( ITagSet tags )
	{
		if ( tags is null ) return 1;
		if ( tags.Has( "head" ) || tags.Has( "hitgroup_1" ) ) return 2;
		return tags.Has( "hitgroup_6" ) || tags.Has( "hitgroup_7" ) || tags.Has( "leg" ) ? 0.5f : 1;
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowAim( bool aiming )
	{
		if ( aiming ) View?.Play( "aim", holdLastFrame: true );
		else View?.Play( "idle", idleAfter: 12.5f );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowFire( ShotEffect shot )
	{
		TimeSinceShoot = 0;
		SetNextPrimaryFire( PrimaryDelay );
		OnShootEffects( shot );
	}

	internal override Vector2 SamplePelletOffset() => Vector2.Zero;
	internal override void DrawShotTrail( Vector3 start, Vector3 end ) { }
	internal override void PlayShotSound() => PlaySample( ShotSound, 0.9f );

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( IsActive && Charging && Owner is { } player )
		{
			if ( Networking.IsHost || !IsProxy ) LimitMovement( player );
			if ( !Application.IsDedicatedServer ) UpdateSpot( player );
			return;
		}
		ClearAimPresentation();
	}

	void LimitMovement( PlayerController player )
	{
		if ( _aimingPlayer == player ) return;
		_aimingPlayer = player;
		FortressSpeed.SetAimLimit( player, AimSpeed );
	}

	void UpdateSpot( PlayerController player )
	{
		if ( !_spot.IsValid() ) _spot = LaserSpot.Create( GameObject, ContentMount, "TFC sniper dot" );
		if ( !_spot.IsValid() ) return;
		var sight = player.EyeTransform.ForwardRay;
		var hit = TraceLineOfFire( sight, Ballistics.Range, 0 ).UseHitboxes().Run();
		_spot.Enabled = hit.Hit;
		_spot.WorldPosition = hit.EndPosition - sight.Forward * 0.5f;
		_spot.Color = Color.White.WithAlpha( (150 + ChargeDamage * 0.25f) / 255 );
	}

	void ClearAimPresentation()
	{
		if ( _aimingPlayer.IsValid() ) FortressSpeed.SetAimLimit( _aimingPlayer, null );
		_aimingPlayer = null;
		if ( _spot.IsValid() ) _spot.Enabled = false;
	}

	void CancelCharge()
	{
		_triggerHeld = false;
		_clicked = false;
		if ( Networking.IsHost ) Charging = false;
		ClearAimPresentation();
	}

	protected override void OnHolstered()
	{
		CancelCharge();
		base.OnHolstered();
	}

	protected override void OnDisabled()
	{
		CancelCharge();
		base.OnDisabled();
	}
}

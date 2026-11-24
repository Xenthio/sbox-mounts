using GoldSrc.HalfLife;

namespace GoldSrc.HalfLifeMMod;

[Title( "Displacer" ), Category( "Half-Life: MMod" )]
[WeaponPickup( "displacer", "uranium", 100, Bucket = 3, Weight = 20, Mount = "hl1mmod" )]
public sealed class Displacer : StudioWeapon
{
	const int CellsPerShot = 10;
	const float ChargeTime = 1;
	const float FireCooldown = 2.5f;

	bool _hasDeployed;
	bool _charging;
	TimeUntil _chargeEnds;
	TimeUntil _nextCharge;
	SoundHandle _chargeSound;

	internal override string DeploySequence => _hasDeployed ? "draw" : "draw_first";
	internal override string ShotSequence => "fire";
	internal override bool CanIdle => !_charging;
	internal override (string Name, float? Delay) IdleAnimation => (Game.Random.Int( 0, 1 ) == 0 ? "idle1" : "idle2", 3);

	internal override void Configure()
	{
		HoldType = WeaponHolds.Rifle;
		UsesClips = false;
		StartingAmmo = 40;
		DeployTime = 0.5f;
		PrimaryDelay = FireCooldown;
	}

	protected override void OnEquipped()
	{
		base.OnEquipped();
		_hasDeployed = true;
	}

	public override bool HasPrimaryAmmo() => !IsHeld || Ammo1 >= CellsPerShot;
	public override bool CanPrimaryAttack() => !_charging && base.CanPrimaryAttack();
	public override bool CanSecondaryAttack() => false;
	public override void PrimaryAttack() => BeginCharge();

	[Rpc.Host( NetFlags.OwnerOnly )]
	void BeginCharge()
	{
		if ( !IsActive || !Owner.IsValid() || _charging || _nextCharge > 0 || !HasPrimaryAmmo() ) return;
		if ( WaterBody.Find( Scene, Owner.EyePosition ).IsValid() )
		{
			_nextCharge = 0.5f;
			RejectCharge();
			return;
		}

		_charging = true;
		_chargeEnds = ChargeTime;
		_nextCharge = FireCooldown;
		ShowCharge();
	}

	public override void DryFire()
	{
		SetNextFire( 0.5f );
		PlaySample( "weapons/357_cock1.wav", 0.8f );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void RejectCharge() => DryFire();

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowCharge()
	{
		SetNextFire( FireCooldown );
		StopChargeSound();
		_chargeSound = PlaySample( "weapons/displacer_spin.wav", Game.Random.Float( 0.8f, 0.9f ) );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( "spinup", holdLastFrame: true );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( _chargeSound.IsValid() ) _chargeSound.Position = GetMuzzleTransform().Position;
		if ( !Networking.IsHost || !_charging || _chargeEnds > 0 ) return;

		_charging = false;
		if ( !IsActive || !Owner.IsValid() || !TakePrimaryAmmo( CellsPerShot ) )
		{
			CancelCharge();
			return;
		}

		var muzzle = OffsetLaunchRay( new Vector3( 16, 12, 8 ) );
		NpcProjectile.Fire<DisplacerBall>( Owner.GameObject, "mount://hl1mmod/hl1mmod/sprites/displacer_ball.spr",
			150, null, muzzle.Position, muzzle.Forward, null );
		ShowFire();
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowFire()
	{
		StopChargeSound();
		PlaySample( "weapons/displacer_fire.wav", Game.Random.Float( 0.8f, 0.9f ) );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( ShotSequence );
		PostWeaponEvent( WeaponEvents.Attack );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void CancelCharge()
	{
		StopChargeSound();
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( "idle1" );
	}

	void StopChargeSound()
	{
		_chargeSound?.Stop();
		_chargeSound = null;
	}

	protected override void OnHolstered()
	{
		_charging = false;
		StopChargeSound();
		base.OnHolstered();
	}

	protected override void OnDisabled()
	{
		_charging = false;
		StopChargeSound();
		base.OnDisabled();
	}
}

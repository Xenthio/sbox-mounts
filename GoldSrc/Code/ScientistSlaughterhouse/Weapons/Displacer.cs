using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

[Title( "Displacer" ), Category( "Scientist Slaughterhouse" )]
[WeaponPickup( "displacer", "uranium", 999, Bucket = 5, Weight = 30, Mount = "ssh" )]
public sealed class Displacer : StudioWeapon
{
	const int ShotCost = 20;
	TimeUntil _charge, _ready;
	bool _charging, _disk;
	SoundHandle _spin;

	internal override string ShotSequence => "fire";
	internal override bool CanIdle => !_charging;
	internal override (string Name, float? Delay) IdleAnimation => Game.Random.Int( 0, 1 ) == 0 ? ("idle1", null) : ("idle2", null);

	internal override void Configure()
	{
		HoldType = WeaponHolds.Rifle;
		UsesClips = false;
		StartingAmmo = 100;
		PrimaryDelay = SecondaryDelay = 2;
	}

	public override bool HasPrimaryAmmo() => !IsHeld || Ammo1 >= ShotCost;
	public override bool HasSecondaryAmmo() => HasPrimaryAmmo();
	public override bool CanPrimaryAttack() => !_charging && base.CanPrimaryAttack();
	public override bool CanSecondaryAttack() => !_charging && base.CanSecondaryAttack();
	public override void PrimaryAttack() => BeginCharge( false );
	public override void SecondaryAttack() => BeginCharge( true );

	[Rpc.Host( NetFlags.OwnerOnly )]
	void BeginCharge( bool disk )
	{
		if ( !IsActive || !Owner.IsValid() || _charging || _ready > 0 || !HasPrimaryAmmo() ) return;
		if ( WaterBody.Find( Scene, Owner.EyePosition ).IsValid() )
		{
			_ready = 0.15f;
			Click( 0.15f );
			return;
		}
		_disk = disk;
		_charging = true;
		_charge = 1;
		_ready = 2;
		ShowCharge( disk );
	}

	public override void DryFire() => PlayClick( 0.5f );

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void Click( float delay ) => PlayClick( delay );

	void PlayClick( float delay )
	{
		SetNextFire( delay );
		PlaySample( "weapons/357_cock1.wav", 0.8f );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowCharge( bool disk )
	{
		SetNextFire( 2 );
		StopChargeSound();
		_spin = PlaySample( disk ? "weapons/displacer_spin2.wav" : "weapons/displacer_spin.wav" );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( "spinup", holdLastFrame: true );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( _spin.IsValid() ) _spin.Position = GetMuzzleTransform().Position;
		if ( !Networking.IsHost || !_charging || _charge > 0 ) return;
		_charging = false;
		if ( !IsActive || !Owner.IsValid() || !TakePrimaryAmmo( ShotCost ) )
		{
			CancelChargePresentation();
			return;
		}

		var muzzle = OffsetLaunchRay( _disk ? new Vector3( 32, 0, -12 ) : new Vector3( 8, 0, -8 ) );
		if ( _disk )
		{
			var target = ProjectileTarget.Find( Scene, Owner.GameObject, muzzle );
			NpcProjectile.Fire<EnergyDisk>( Owner.GameObject, "mount://ssh/ssh/models/disk.mdl.vmdl", 100, target, muzzle.Position, muzzle.Forward, 1 );
		}
		else
			NpcProjectile.Fire<DisplacerBall>( Owner.GameObject, "mount://ssh/valve/sprites/exit1.spr", 250, null, muzzle.Position, muzzle.Forward, 1000 );
		ShowFire();
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowFire()
	{
		StopChargeSound();
		PlaySample( "weapons/displacer_fire.wav" );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( ShotSequence, idleAfter: 1 );
		PostWeaponEvent( WeaponEvents.Attack );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void CancelChargePresentation()
	{
		StopChargeSound();
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( "idle1" );
	}

	void StopChargeSound()
	{
		_spin?.Stop();
		_spin = null;
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

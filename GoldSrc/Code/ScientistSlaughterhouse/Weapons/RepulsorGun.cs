using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

[Title( "Repulsor Gun" ), Category( "Scientist Slaughterhouse" )]
[WeaponPickup( "gauss", "rep", 16, ClassName = "repulsor", Bucket = 1, Weight = 1, Mount = "ssh" )]
public sealed partial class RepulsorGun : StudioWeapon
{
	const float RechargeInterval = 0.5f;
	const float ShotDelay = 0.3f;
	TimeUntil _nextCharge, _ready;

	internal override string ShotSequence => "fire";
	internal override bool CanIdle => !TractorTarget.IsValid();
	internal override (string Name, float? Delay) IdleAnimation => Game.Random.Float() switch
	{
		<= 0.5f => ("idle", Game.Random.Float( 10, 15 )),
		<= 0.75f => ("idle2", Game.Random.Float( 10, 15 )),
		_ => ("fidget", 3)
	};
	internal override Vector3 WorldMuzzleOffset( bool hd ) => new( 28.4f, 2.8f, hd ? 4.6f : 4.1f );

	internal override void Configure()
	{
		HoldType = WeaponHolds.Rifle;
		UsesClips = false;
		StartingAmmo = 16;
		PrimaryDelay = SecondaryDelay = ShotDelay;
		PrimaryAutomatic = SecondaryAutomatic = true;
	}

	public override bool HasPrimaryAmmo() => TractorTarget.IsValid() || base.HasPrimaryAmmo();
	public override bool HasSecondaryAmmo() => TractorTarget.IsValid() || !IsHeld || Ammo1 >= 2;
	public override void PrimaryAttack() => Fire( false );
	public override void SecondaryAttack() => Fire( true );

	[Rpc.Host( NetFlags.OwnerOnly )]
	void Fire( bool charged )
	{
		if ( !IsActive || !Owner.IsValid() || _ready > 0 ) return;
		if ( TractorTarget.IsValid() )
		{
			_tractorDistance += charged ? -4 : 4;
			if ( _tractorDistance > MaxTractorDistance ) _tractorDistance = MinTractorDistance;
			_tractorDistance = System.MathF.Max( MinTractorDistance, _tractorDistance );
			_ready = 0.025f;
			SetDelay( 0.025f );
			return;
		}
		if ( WaterBody.Find( Scene, Owner.EyePosition ).IsValid() )
		{
			_ready = 0.15f;
			Click();
			return;
		}
		if ( !TakePrimaryAmmo( charged ? 2 : 1 ) ) return;

		var ray = OffsetLaunchRay( new Vector3( 0, 10, 7 ) );
		NpcProjectile.Fire<RepulsorPulse>( Owner.GameObject, "mount://ssh/valve/sprites/xspark4.spr",
			charged ? 35 : 0, null, ray.Position, ray.Forward, 1000 );
		_ready = ShotDelay;
		_nextCharge = RechargeInterval;
		ShowShot();
	}

	public override void DryFire() => SetNextFire( 0.5f );

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void Click()
	{
		SetNextFire( 0.15f );
		PlaySample( "weapons/357_cock1.wav" );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void SetDelay( float delay ) => SetNextFire( delay );

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowShot()
	{
		SetNextFire( ShotDelay );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( ShotSequence, idleAfter: 1 );
		PlaySample( "weapons/ric1.wav", pitch: Game.Random.Float( 0.8f, 1.1f ), attenuation: 0.8f );
		PostWeaponEvent( WeaponEvents.Attack );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		UpdateTractorView();
		if ( !Networking.IsHost || !IsActive || !Inventory.IsValid() || TractorTarget.IsValid() || Ammo1 >= MaxReserveAmmo || _nextCharge > 0 ) return;
		var rounds = 1 + (int)(-(float)_nextCharge / RechargeInterval);
		_nextCharge = (float)_nextCharge + rounds * RechargeInterval;
		Inventory.GiveAmmo( PrimaryAmmoType, rounds );
	}

	protected override void OnHolstered()
	{
		if ( Networking.IsHost ) ReleaseTractor();
		base.OnHolstered();
	}

	protected override void OnDisabled()
	{
		if ( Networking.IsHost ) ReleaseTractor();
		base.OnDisabled();
	}
}

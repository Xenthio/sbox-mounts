namespace GoldSrc.HalfLife;

[Title( "Tau Cannon" ), Category( "Half-Life" )]
[WeaponPickup( "gauss", "uranium", 100, Bucket = 3, Weight = 20 )]
public sealed class GaussGun : ClassicGaussGun;

[Hide]
public abstract partial class ClassicGaussGun : StudioWeapon
{
	static float FullChargeTime => Networking.MaxPlayers > 1 ? 1.5f : 4;
	bool _charging, _released, _triggerHeld, _spinning;
	TimeSince _chargeAge, _soundAge;
	TimeUntil _ready;
	TimeUntil? _aftershock;
	float _nextBurn;
	SoundHandle _spinSound;

	internal override string ShotSequence => "fire";
	internal override bool SwitchWhenSpent => !_triggerHeld && !_charging && !_spinning;
	internal override (string Name, float? Delay) IdleAnimation => (_spinning ? "spin" : "idle", null);
	internal override Vector3 WorldMuzzleOffset( bool hd ) => new( 28.4f, 2.8f, hd ? 4.6f : 4.1f );
	public override Ray AimRay => Owner.IsValid() ? Owner.EyeTransform.ForwardRay : base.AimRay;

	internal override void Configure()
	{
		HoldType = WeaponHolds.Rifle;
		PrimaryDelay = 0.2f;
		SecondaryDelay = 0.1f;
		UsesClips = false;
		StartingAmmo = 20;
		Ballistics = new() { Damage = 20, Range = 8192 };
		AttackDamageTags = ["bullet"];
	}

	public override bool HasPrimaryAmmo() => !IsHeld || Ammo1 >= 2;
	public override bool HasSecondaryAmmo() => !IsHeld || Ammo1 >= 1;
	public override void DryFire()
	{
		PlaySample( "weapons/357_cock1.wav", 0.8f );
		SetNextFire( 0.5f );
	}

	public override void PrimaryAttack()
	{
		if ( Owner.IsValid() ) StartAttack( false );
		else base.PrimaryAttack();
	}

	internal override void DrawShotTrail( Vector3 start, Vector3 end ) =>
		EnergyBeam.Show( start, end, new Color( 1, 0.5f, 0, 0.5f ), "smoke", "Gauss beam", 1, 0.1f, ContentMount );

	internal override void PlayShotSound() => PlaySample( "weapons/gauss2.wav", 0.55f, Game.Random.Float( 0.85f, 1.16f ) );

	public override void SecondaryAttack()
	{
		_triggerHeld = true;
		StartAttack( true );
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void StartAttack( bool charge )
	{
		if ( !IsActive || !Owner.IsValid() || _charging || _ready > 0 ) return;
		if ( WaterBody.Find( Scene, Owner.EyePosition ).IsValid() )
		{
			_ready = charge ? 0.5f : 0.15f;
			PlayDryFire();
			return;
		}
		if ( !TakePrimaryAmmo( charge ? 1 : 2 ) ) return;
		if ( !charge )
		{
			_ready = PrimaryDelay;
			FireBeam( Ballistics.Damage, false );
			return;
		}
		_charging = true;
		_released = false;
		_chargeAge = 0;
		_nextBurn = 0.5f;
		SpinUp();
	}

	protected override void Think()
	{
		base.Think();
		if ( !_triggerHeld || Input.Down( "attack2" ) ) return;
		_triggerHeld = false;
		ReleaseTrigger();
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void ReleaseTrigger() => _released = true;

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( !IsHeld )
		{
			CancelCharge();
			return;
		}
		if ( _spinSound.IsValid() )
		{
			_spinSound.Position = WorldPosition;
			_spinSound.Pitch = _spinning ? System.MathF.Min( 2.5f, 1 + _soundAge * 1.5f / FullChargeTime ) : 1.1f;
		}
		if ( !Networking.IsHost || !Owner.IsValid() ) return;
		if ( _aftershock is { } due && due <= 0 )
		{
			_aftershock = null;
			var sample = Game.Random.Int( 4, 7 );
			if ( sample <= 6 ) Aftershock( sample );
		}
		if ( !_charging ) return;
		if ( WaterBody.Find( Scene, Owner.EyePosition ).IsValid() || _chargeAge > 10 )
		{
			bool overload = _chargeAge > 10;
			_charging = false;
			_ready = overload ? 1 : 0.5f;
			Discharge( overload );
			if ( overload && Owner.GameObject.GetComponentInParent<IDamageable>() is { } victim )
			{
				var damage = new DamageInfo { Damage = 50, Position = Owner.EyePosition, Weapon = GameObject };
				damage.Tags.Add( "shock" );
				victim.OnDamage( damage );
			}
			return;
		}
		if ( _chargeAge < 0.5f ) return;
		if ( !_released && _nextBurn <= FullChargeTime + 0.001f && _chargeAge >= _nextBurn )
		{
			if ( _nextBurn == 0.5f ) Spin();
			_nextBurn += Networking.MaxPlayers > 1 ? 0.1f : 0.3f;
			TakePrimaryAmmo();
		}
		if ( !_released && Ammo1 > 0 ) return;
		_charging = false;
		_ready = Ammo1 > 0 ? PrimaryDelay : 1;
		FireBeam( 200 * System.MathF.Min( 1, _chargeAge / FullChargeTime ), true );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void PlayDryFire() => DryFire();

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void SpinUp()
	{
		StopSpin();
		_soundAge = 0;
		_spinSound = PlaySample( "ambience/pulsemachine.wav", pitch: 1.1f );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( "spinup", true );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void Spin()
	{
		_spinning = true;
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( "spin" );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void Aftershock( int sample ) => PlaySample( $"weapons/electro{sample}.wav", Game.Random.Float( 0.7f, 0.8f ) );

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void Discharge( bool overload )
	{
		StopSpin();
		PlaySample( "weapons/electro4.wav", pitch: Game.Random.Float( 0.8f, 1.43f ) );
		if ( overload )
		{
			PlaySample( "weapons/electro6.wav", pitch: Game.Random.Float( 0.75f, 1.38f ) );
			if ( !IsProxy && Owner.IsValid() && !Application.IsDedicatedServer ) GaussOverchargeFlash.Show();
		}
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( "idle" );
	}

	void StopSpin()
	{
		_spinning = false;
		_spinSound?.Stop();
		_spinSound = null;
	}

	void CancelCharge()
	{
		_aftershock = null;
		_charging = _triggerHeld = false;
		StopSpin();
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

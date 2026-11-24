using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

[Title( "Reflection Laser" ), Category( "Scientist Slaughterhouse" )]
[WeaponPickup( "gauss", "uranium", 999, ClassName = "reflector", Bucket = 5, Weight = 15, Mount = "ssh" )]
public sealed partial class ReflectionLaser : StudioWeapon
{
	const float PulseInterval = 0.02f;
	const float BeamRange = 4096;
	const float FuelInterval = 0.166f;
	const float InputTimeout = 0.16f;

	TimeUntil _pulseReady, _windup, _nextFuel, _inputDeadline, _nextVisual, _restart;
	bool _firing;

	internal override bool CanIdle => !_presenting;
	internal override bool SwitchWhenSpent => !_presenting;
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
		StartingAmmo = 100;
		PrimaryAutomatic = SecondaryAutomatic = true;
		PrimaryDelay = SecondaryDelay = PulseInterval;
		Ballistics = new() { Range = BeamRange, Radius = 0 };
	}

	public override bool HasSecondaryAmmo() => HasPrimaryAmmo();
	public override void PrimaryAttack() => Fire( false );
	public override void SecondaryAttack() => Fire( true );

	[Rpc.Host( NetFlags.OwnerOnly )]
	void Fire( bool cutting )
	{
		if ( !IsActive || !Owner.IsValid() || _pulseReady > 0 || _restart > 0 ) return;
		if ( WaterBody.Find( Scene, Owner.EyePosition ).IsValid() || !HasPrimaryAmmo() )
		{
			StopFiring();
			_restart = 0.25f;
			Click();
			return;
		}

		_inputDeadline = InputTimeout;
		_pulseReady = PulseInterval;
		if ( !_firing )
		{
			_firing = true;
			_windup = 0.1f;
			BeginPresentation();
		}
		if ( _windup > 0 ) return;

		var burn = _nextFuel <= 0;
		if ( burn )
		{
			if ( !TakePrimaryAmmo() ) { StopFiring( 1 ); return; }
			_nextFuel = FuelInterval;
		}

		var draw = _nextVisual <= 0;
		var segments = draw ? new List<Vector3>( cutting ? 2 : 30 ) : null;
		TraceBeams( Owner.EyeTransform.ForwardRay, cutting, burn, segments );
		if ( draw )
		{
			_nextVisual = 0.04f;
			ShowBeams( segments.ToArray(), cutting );
		}
		if ( !HasPrimaryAmmo() ) StopFiring( 1 );
	}

	protected override void Think()
	{
		base.Think();
		if ( _presenting && !Input.Down( "attack1" ) && !Input.Down( "attack2" ) &&
			(Input.Released( "attack1" ) || Input.Released( "attack2" )) )
			ReleaseTrigger();
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void ReleaseTrigger() => StopFiring();

	void StopFiring( float delay = 0.5f )
	{
		if ( !_firing ) return;
		_firing = false;
		_nextFuel = _nextVisual = 0;
		_restart = delay;
		EndPresentation( delay );
	}

	public override void DryFire() => PlayClick();

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void Click() => PlayClick();

	void PlayClick()
	{
		SetNextFire( 0.25f );
		PlaySample( "weapons/357_cock1.wav", attenuation: 0.8f );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( Networking.IsHost && _firing && _inputDeadline <= 0 ) StopFiring();
		UpdatePresentation();
	}

	protected override void OnHolstered()
	{
		if ( Networking.IsHost ) StopFiring();
		StopPresentation();
		base.OnHolstered();
	}

	protected override void OnDisabled()
	{
		_firing = false;
		_nextFuel = _nextVisual = 0;
		StopPresentation();
		base.OnDisabled();
	}
}

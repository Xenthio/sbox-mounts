namespace GoldSrc.HalfLife;

[Hide]
public abstract class PinGrenadeWeapon : StudioWeapon
{
	bool _holdingPin, _primed, _redrawing;
	TimeSince _pinAge;
	TimeUntil _redrawAt;
	bool _released, _holdingSecondary;

	internal bool Underhand { get; private set; }

	internal abstract string PinSequence { get; }
	internal virtual string PinSound => null;
	internal virtual string ThrowSound => null;
	internal virtual bool HasGrenadesLeft => HasPrimaryAmmo();
	internal virtual float MinimumPinTime => 0.5f;
	internal virtual float HeldFuse => float.PositiveInfinity;
	internal virtual float RedrawDelay => 0.5f;
	internal abstract string LaunchGrenade( Vector3 origin, Vector3 direction, float pitch, float pinAge );

	internal override void Configure()
	{
		HoldType = WeaponHolds.Throwable;
		PrimaryDelay = 0.5f;
		PrimaryAutomatic = false;
		UsesClips = false;
	}

	protected bool CanPrime => !_holdingPin && !_primed && !_redrawing && HasPrimaryAmmo();
	public override bool CanPrimaryAttack() => CanPrime && base.CanPrimaryAttack();
	protected override bool OnHolstering( BaseInventoryItem next ) => !_holdingPin && !_primed && base.OnHolstering( next );

	public override void PrimaryAttack() => Prime( false );

	protected void Prime( bool underhand )
	{
		_holdingPin = true;
		_holdingSecondary = underhand;
		PullPin( underhand );
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void PullPin( bool underhand )
	{
		if ( !IsActive || !Owner.IsValid() || _primed || _redrawing || !HasPrimaryAmmo() ) return;
		_primed = true;
		Underhand = underhand;
		_pinAge = 0;
		ShowAnimation( PinSequence );
	}

	protected override void Think()
	{
		base.Think();
		if ( !_holdingPin || Input.Down( _holdingSecondary ? "attack2" : "attack1" ) ) return;
		_holdingPin = false;
		ReleasePin();
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void ReleasePin()
	{
		if ( IsActive && _primed && Owner.IsValid() ) _released = true;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( !Networking.IsHost || !IsActive || !Owner.IsValid() ) return;
		if ( _primed && (_pinAge >= HeldFuse || (_released && _pinAge >= MinimumPinTime)) ) Throw();
		if ( !_redrawing || _redrawAt > 0 ) return;
		_redrawing = false;
		if ( HasGrenadesLeft ) ShowAnimation( DeploySequence );
		else Inventory.Remove( this );
	}

	void Throw()
	{
		_primed = false;
		_released = false;
		var angles = Owner.EyeAngles;
		angles.pitch = -10 + angles.pitch * (angles.pitch < 0 ? 80f : 100f) / 90;
		var direction = angles.ToRotation().Forward;
		var origin = LaunchOrigin( direction );
		var animation = LaunchGrenade( origin, direction, angles.pitch, _pinAge );
		TakePrimaryAmmo();
		ShowAnimation( animation, attack: true );
		_redrawing = true;
		_redrawAt = HasGrenadesLeft ? RedrawDelay : 0.5f;
	}

	internal virtual Vector3 LaunchOrigin( Vector3 direction ) =>
		BulletTrace( new Ray( Owner.EyePosition, direction ), 16, 2 ).Run().EndPosition;

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowAnimation( string name, bool attack = false )
	{
		if ( attack )
		{
			SetNextPrimaryFire( PrimaryDelay );
			PostWeaponEvent( WeaponEvents.Attack );
			if ( ThrowSound is { } throwSound ) PlaySample( throwSound );
		}
		if ( name == PinSequence && PinSound is { } sample ) PlaySample( sample );
		var view = ViewModel?.GetComponent<StudioWeaponView>();
		if ( name == DeploySequence ) view?.Play( name, idleAfter: Game.Random.Float( 10, 15 ) );
		else view?.Play( name, holdLastFrame: true );
	}

	protected override void OnHolstered()
	{
		_holdingPin = _primed = _redrawing = false;
		_released = false;
		base.OnHolstered();
	}
}

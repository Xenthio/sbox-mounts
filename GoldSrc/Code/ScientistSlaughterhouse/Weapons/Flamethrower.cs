using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

[Title( "Flamethrower" ), Category( "Scientist Slaughterhouse" )]
[WeaponPickup( "egon", "ffuel", 250, ClassName = "flamethrower", Bucket = 3, Weight = 25, Mount = "ssh" )]
public sealed class Flamethrower : StudioWeapon
{
	internal const string AcidAmmoPath = "ammo/acid.ammo";
	const float PulseInterval = 0.05f;
	TimeUntil _primaryReady, _secondaryReady, _nextFuel, _streamExpires;
	bool _streaming;
	SoundHandle _loop;

	internal override string ShotSequence => $"fire{Game.Random.Int( 1, 4 )}";
	internal override bool CanIdle => !_streaming;
	internal override (string Name, float? Delay) IdleAnimation =>
		Game.Random.Int( 0, 1 ) == 0 ? ("idle1", Game.Random.Float( 10, 15 )) : ("fidget1", 3);
	public override Ray AimRay => Owner.IsValid() ? OffsetLaunchRay( new Vector3( 16, -3, -8 ) ) : base.AimRay;

	internal override void Configure()
	{
		HoldType = WeaponHolds.Rifle;
		UsesClips = false;
		StartingAmmo = 20;
		PrimaryDelay = SecondaryDelay = PulseInterval;
		PrimaryAutomatic = SecondaryAutomatic = true;
	}

	internal override void BindAmmoTypes()
	{
		base.BindAmmoTypes();
		SecondaryAmmoType ??= GameResource.Load<BaseAmmoResource>( $"mount://ssh/{AcidAmmoPath}" );
	}

	public override void PrimaryAttack() => EmitPulse( AimRay, false );
	public override void SecondaryAttack() => EmitPulse( AimRay, true );

	[Rpc.Host( NetFlags.OwnerOnly )]
	void EmitPulse( Ray aim, bool acid )
	{
		if ( !IsActive || !Owner.IsValid() || (acid ? _secondaryReady : _primaryReady) > 0 ||
			!aim.Position.IsFinite || !aim.Forward.IsFinite || aim.Position.Distance( Owner.EyePosition ) > 96 ) return;

		if ( WaterBody.Find( Scene, Owner.EyePosition ).IsValid() )
		{
			StopStream();
			_primaryReady = _secondaryReady = 0.15f;
			ClickUnderwater();
			return;
		}

		if ( acid ? !HasSecondaryAmmo() : !HasPrimaryAmmo() ) return;
		if ( _nextFuel <= 0 )
		{
			if ( !(acid ? TakeSecondaryAmmo() : TakePrimaryAmmo()) ) return;
			_nextFuel = 0.2f;
		}

		if ( acid )
			NpcProjectile.Fire<AcidFlame>( Owner.GameObject, "mount://ssh/ssh/sprites/bio_blast.spr", 5, null, aim.Position, aim.Forward, 240 );
		else
			NpcProjectile.Fire<FlyingFlame>( Owner.GameObject, "mount://ssh/ssh/sprites/flamer.spr", 5, null, aim.Position, aim.Forward, 240 );

		var start = _streamExpires <= 0;
		_streamExpires = 0.15f;
		_primaryReady = acid ? 1 : PulseInterval;
		_secondaryReady = acid ? PulseInterval : 1;
		ShowPulse( acid, start );
	}

	protected override void Think()
	{
		base.Think();
		if ( _streaming && !Input.Down( "attack1" ) && !Input.Down( "attack2" ) &&
			(Input.Released( "attack1" ) || Input.Released( "attack2" )) )
			StopStream();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( Networking.IsHost && _streaming && _streamExpires <= 0 ) StopStream();
		if ( _loop.IsValid() ) _loop.Position = GetMuzzleTransform().Position;
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void StopStream()
	{
		_nextFuel = _streamExpires = 0;
		if ( _streaming ) EndPresentation();
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowPulse( bool acid, bool start )
	{
		SetNextPrimaryFire( acid ? 1 : PulseInterval );
		SetNextSecondaryFire( acid ? PulseInterval : 1 );
		if ( !start ) return;
		_streaming = true;
		PlaySample( "garg/gar_flameon1.wav", attenuation: 0.8f );
		_loop?.Stop();
		_loop = PlaySample( "garg/gar_flamerun1.wav", attenuation: 0.8f );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( ShotSequence, idleAfter: 1 );
		PostWeaponEvent( WeaponEvents.Attack );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void EndPresentation() => StopPresentation( true );

	void StopPresentation( bool playTail )
	{
		_loop?.Stop();
		_loop = null;
		if ( _streaming && playTail ) PlaySample( "garg/gar_flameoff1.wav", attenuation: 0.8f );
		_streaming = false;
	}

	protected override void OnHolstered()
	{
		if ( Networking.IsHost ) StopStream();
		StopPresentation( true );
		base.OnHolstered();
	}

	protected override void OnDisabled()
	{
		_nextFuel = _streamExpires = 0;
		StopPresentation( false );
		base.OnDisabled();
	}

	public override void DryFire() => PlayClick( 0.5f );

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ClickUnderwater() => PlayClick( 0.15f );

	void PlayClick( float delay )
	{
		SetNextFire( delay );
		PlaySample( "weapons/357_cock1.wav", attenuation: 0.8f );
	}
}

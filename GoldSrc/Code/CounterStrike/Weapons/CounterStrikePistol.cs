using System;
using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Hide]
public abstract class CounterStrikePistol : CounterStrikeFirearm
{
	internal readonly record struct PistolAccuracy( float Maximum, float Minimum, float RecoveryTime, float Rate );

	float? _accuracy;
	float? _lastShot;

	internal abstract PistolAccuracy Accuracy { get; }
	internal virtual int FireSequenceCount => 2;
	internal virtual float AirborneSpread => 1.5f;
	internal virtual float MovingSpread => 0.255f;
	internal virtual float CrouchingSpread => 0.075f;
	internal virtual float StandingSpread => 0.15f;
	internal virtual float RecoilPitch => -2;

	internal override string ShotSequence => Clip1 == 0 ? "shoot_empty" : $"shoot{Game.Random.Int( 1, FireSequenceCount )}";
	internal override float? IdleAfterShot => 2;
	internal override float IdleAfterReloadStart => ReloadTime;
	internal override string ShotSound => $"weapons/{Definition.Model}-1.wav";

	internal override void Configure()
	{
		HoldType = WeaponHolds.Pistol;
		Handedness = WeaponHandedness.Right;
		DeployTime = 0.75f;
		PrimaryAutomatic = false;
		AttackDamageTags = ["bullet"];
	}

	public override void PrimaryAttack() => FirePistolRound();

	protected bool FirePistolRound()
	{
		if ( IsHeld && !TakePrimaryAmmo() ) return false;

		var spread = Spread();
		var now = Time.Now;
		var sinceLastShot = _lastShot is { } last ? now - last : float.PositiveInfinity;
		if ( _lastShot.HasValue )
		{
			var accuracy = Accuracy;
			var penalty = (accuracy.RecoveryTime - sinceLastShot) * accuracy.Rate;
			_accuracy = Math.Clamp( (_accuracy ?? accuracy.Maximum) - penalty, accuracy.Minimum, accuracy.Maximum );
		}
		OnRoundFiring( sinceLastShot );
		_lastShot = now;
		FireRound( spread );
		return true;
	}

	internal virtual void OnRoundFiring( float sinceLastShot ) { }

	float Spread()
	{
		var scale = Owner switch
		{
			{ IsOnGround: false } => AirborneSpread,
			{ } player when player.Velocity.WithZ( 0 ).LengthSquared > 0 => MovingSpread,
			{ IsDucking: true } => CrouchingSpread,
			_ => StandingSpread
		};
		return scale * (1 - (_accuracy ?? Accuracy.Maximum));
	}

	protected override void OnReloadStarted()
	{
		base.OnReloadStarted();
		_accuracy = null;
	}

	protected override void OnHolstered()
	{
		base.OnHolstered();
		_accuracy = null;
	}

	protected override void OnShootEffects( ShotEffect shot )
	{
		base.OnShootEffects( shot );
		if ( shot.NoEvents ) return;
		EjectPistolCasing();
		if ( RecoilPitch != 0 && !IsProxy && Owner is { ThirdPerson: false } )
			MeleeViewPunch.Apply( Scene.Camera, new Angles( RecoilPitch, 0, 0 ) );
	}

	internal virtual void EjectPistolCasing() => EjectShells( 1, false, "models/pshell.mdl" );

	public override void DryFire()
	{
		PlaySample( "weapons/dryfire_pistol.wav" );
		base.DryFire();
	}
}

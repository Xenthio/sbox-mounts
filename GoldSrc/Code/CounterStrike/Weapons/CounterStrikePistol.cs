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
	internal virtual float MovingSpread => 0.255f;
	internal virtual float CrouchingSpread => 0.075f;
	internal virtual float StandingSpread => 0.15f;

	internal override string ShotSequence => Clip1 == 0 ? "shoot_empty" : $"shoot{Game.Random.Int( 1, FireSequenceCount )}";
	internal override float? IdleAfterShot => 2;
	internal override float IdleAfterReloadStart => ReloadTime;
	internal override string ShotSound => $"weapons/{Definition.Model}-1.wav";

	internal override void Configure()
	{
		HoldType = WeaponHolds.Pistol;
		Handedness = WeaponHandedness.Right;
		PrimaryAutomatic = false;
		AttackDamageTags = ["bullet"];
	}

	public override void PrimaryAttack()
	{
		if ( IsHeld && !TakePrimaryAmmo() ) return;

		var spread = Spread();
		if ( _lastShot is { } last )
		{
			var accuracy = Accuracy;
			var penalty = (accuracy.RecoveryTime - (Time.Now - last)) * accuracy.Rate;
			_accuracy = Math.Clamp( (_accuracy ?? accuracy.Maximum) - penalty, accuracy.Minimum, accuracy.Maximum );
		}
		_lastShot = Time.Now;
		FireRound( spread );
	}

	float Spread()
	{
		var scale = Owner switch
		{
			{ IsOnGround: false } => 1.5f,
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
		if ( !IsProxy && Owner is { ThirdPerson: false } )
			MeleeViewPunch.Apply( Scene.Camera, new Angles( -2, 0, 0 ) );
	}

	public override void DryFire()
	{
		PlaySample( "weapons/dryfire_pistol.wav" );
		base.DryFire();
	}
}

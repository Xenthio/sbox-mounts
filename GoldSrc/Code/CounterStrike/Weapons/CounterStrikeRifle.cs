using System;
using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Hide]
public abstract class CounterStrikeRifle : CounterStrikeFirearm
{
	int _shotsFired;
	float _accuracy = 0.2f;
	TimeUntil? _nextRecovery;

	internal abstract float AccuracyDivisor { get; }
	internal virtual float AccuracyOffset => 0.3f;
	internal virtual float MaxAccuracy => 1;
	internal virtual float ReloadAccuracy => 0.2f;
	internal virtual float StandingSpread => 0.02f;
	internal virtual float AirborneSpread => 0.4f;
	internal virtual float MovingSpread => 0.07f;
	internal virtual float MovingPenalty => 0.035f;
	internal virtual float Recoil => 1;
	internal virtual float FireDelay => PrimaryDelay;
	internal virtual bool SupportsSecondary => false;

	internal override string ShotSequence => $"shoot{Game.Random.Int( 1, 3 )}";
	internal override string ShotSound => $"weapons/{Definition.Model}-1.wav";
	internal override (string Name, float? Delay) IdleAnimation => ("idle", 20);
	internal override float? IdleAfterShot => 1.9f;
	internal override float IdleAfterReloadStart => ReloadTime;

	internal override void Configure()
	{
		HoldType = WeaponHolds.Rifle;
		Handedness = WeaponHandedness.Right;
		PrimaryAutomatic = true;
		SecondaryAutomatic = false;
		ClipMaxSize = 30;
		AttackDamageTags = ["bullet"];
	}

	public override bool CanSecondaryAttack() => SupportsSecondary && NextSecondaryFire <= 0 && !IsReloading;

	public override void PrimaryAttack()
	{
		if ( IsHeld && !TakePrimaryAmmo() ) return;

		var spread = Owner switch
		{
			{ IsOnGround: false } => MovingPenalty + AirborneSpread * _accuracy,
			{ } player when player.Velocity.WithZ( 0 ).LengthSquared > 140 * 140 => MovingPenalty + MovingSpread * _accuracy,
			_ => StandingSpread * _accuracy
		};

		++_shotsFired;
		_accuracy = MathF.Min( MaxAccuracy, _shotsFired * _shotsFired * _shotsFired / AccuracyDivisor + AccuracyOffset );
		_nextRecovery = null;
		SetNextFire( FireDelay );
		FireRound( spread );
	}

	protected override void Think()
	{
		base.Think();
		if ( _shotsFired == 0 || Input.Down( "attack1" ) ) return;
		if ( _nextRecovery is null )
		{
			_shotsFired = Math.Min( _shotsFired, 15 );
			_nextRecovery = 0.4f;
			return;
		}
		if ( _nextRecovery.Value > 0 ) return;
		_nextRecovery = 0.0225f;
		if ( --_shotsFired == 0 ) _accuracy = 0.2f;
	}

	protected override void OnReloadStarted()
	{
		base.OnReloadStarted();
		_shotsFired = 0;
		_accuracy = ReloadAccuracy;
		_nextRecovery = null;
	}

	protected override void OnHolstered()
	{
		base.OnHolstered();
		_shotsFired = 0;
		_accuracy = 0.2f;
		_nextRecovery = null;
	}

	protected override void OnShootEffects( ShotEffect shot )
	{
		base.OnShootEffects( shot );
		if ( !shot.NoEvents && !IsProxy && Owner is { ThirdPerson: false } )
			MeleeViewPunch.Apply( Scene.Camera, new Angles( -Recoil, 0, 0 ) );
	}

	public override void DryFire()
	{
		PlaySample( "weapons/dryfire_rifle.wav" );
		SetNextFire( 0.2f );
	}
}

[Hide]
public abstract class ScopedCounterStrikeRifle : CounterStrikeRifle
{
	internal override float ZoomFieldOfView => 55;
	internal override float FireDelay => Zoomed ? 0.135f : PrimaryDelay;
	internal override bool SupportsSecondary => true;

	internal override void Configure()
	{
		base.Configure();
		PrimaryDelay = 0.0825f;
		SecondaryDelay = 0.3f;
	}
}

using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Title( "FAMAS" ), Category( "Counter-Strike" )]
[WeaponPickup( "famas", "556nato", 90, Bucket = 0, Weight = 75, Mount = "cstrike" )]
public sealed class Famas : FamasWeapon;

[Title( "FAMAS" ), Category( "Counter-Strike: Condition Zero" )]
[WeaponPickup( "famas", "556nato", 90, Bucket = 0, Weight = 75, Mount = "czero" )]
public sealed class ConditionZeroFamas : FamasWeapon;

[Hide]
public abstract class FamasWeapon : CounterStrikeRifle
{
	int _burstRoundsRemaining;
	TimeUntil _nextBurstRound;
	float _burstSpread;

	[Sync] public bool BurstMode { get; private set; }

	internal override float AccuracyDivisor => 215;
	internal override float ReloadAccuracy => 0;
	internal override float AirborneSpread => 0.3f;
	internal override float MovingPenalty => 0.03f;
	internal override float RangeModifier => 0.96f;
	internal override float ShotDamage => BurstMode ? 34 : base.ShotDamage;
	internal override float FireDelay => BurstMode ? 0.55f : PrimaryDelay;
	internal override bool SupportsSecondary => true;
	internal override float? IdleAfterShot => 1.1f;
	internal override string ShotSound => "weapons/famas-";

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 25;
		PrimaryDelay = 0.0825f;
		SecondaryDelay = 0.3f;
		ReloadTime = 3.3f;
		Ballistics = new() { Damage = 30, Range = 8192, Radius = 0 };
	}

	public override void SecondaryAttack() => BurstMode = !BurstMode;

	public override bool CanPrimaryAttack() => _burstRoundsRemaining == 0 && base.CanPrimaryAttack();

	public override bool CanSecondaryAttack() => _burstRoundsRemaining == 0 && base.CanSecondaryAttack();

	public override bool CanReload() => _burstRoundsRemaining == 0 && NextPrimaryFire <= 0 && base.CanReload();

	public override void PrimaryAttack()
	{
		if ( Owner.IsValid() && WaterBody.Find( Scene, Owner.EyePosition ).IsValid() )
		{
			PlaySample( "weapons/dryfire_rifle.wav" );
			SetNextPrimaryFire( 0.15f );
			return;
		}

		if ( FireRifleRound( out _burstSpread, BurstMode ? 0 : 0.01f ) && BurstMode )
		{
			_burstRoundsRemaining = 2;
			_nextBurstRound = 0.05f;
		}
	}

	protected override void Think()
	{
		base.Think();
		if ( _burstRoundsRemaining == 0 || _nextBurstRound > 0 ) return;
		if ( !IsHeld || IsReloading || !TakePrimaryAmmo() )
		{
			_burstRoundsRemaining = 0;
			return;
		}

		--_burstRoundsRemaining;
		_nextBurstRound = 0.1f;
		// Follow-up rounds keep the opening spread and use the normal shot damage.
		FireRound( _burstSpread, Ballistics.Damage );
	}

	protected override void OnHolstered()
	{
		_burstRoundsRemaining = 0;
		base.OnHolstered();
	}
}

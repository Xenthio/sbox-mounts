using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Title( "Glock-18" ), Category( "Counter-Strike" )]
[WeaponPickup( "glock18", "9mm", 120, Bucket = 1, Weight = 5, Mount = "cstrike" )]
public sealed class Glock18 : Glock18Weapon;

[Title( "Glock-18" ), Category( "Counter-Strike: Condition Zero" )]
[WeaponPickup( "glock18", "9mm", 120, Bucket = 1, Weight = 5, Mount = "czero" )]
public sealed class ConditionZeroGlock18 : Glock18Weapon;

[Hide]
public abstract class Glock18Weapon : CounterStrikePistol
{
	[Sync] public bool BurstMode { get; private set; }
	int _burstRounds;
	TimeUntil _nextBurstRound;

	internal override PistolAccuracy Accuracy => new( 0.9f, 0.6f, 0.325f, 0.275f );
	internal override float AirborneSpread => BurstMode ? 1.2f : 1;
	internal override float MovingSpread => BurstMode ? 0.185f : 0.165f;
	internal override float CrouchingSpread => BurstMode ? 0.095f : 0.075f;
	internal override float StandingSpread => BurstMode ? 0.3f : 0.1f;
	internal override float RangeModifier => 0.75f;
	internal override float RecoilPitch => 0;
	internal override float? IdleAfterShot => 2.5f;
	internal override bool CanIdle => Clip1 > 0;
	internal override string DeploySequence => Game.Random.Int( 0, 1 ) == 0 ? "draw" : "draw2";
	internal override string ReloadSequence => Game.Random.Int( 0, 1 ) == 0 ? "reload" : "reload2";
	internal override string ShotSequence => Clip1 == 0 ? "shoot_empty" : "shoot3";
	internal override string ShotSound => "weapons/glock18-2.wav";
	internal override (string Name, float? Delay) IdleAnimation => Game.Random.Float( 0, 1 ) switch
	{
		<= 0.3f => ("idle3", 3.0625f),
		<= 0.6f => ("idle1", 3.75f),
		_ => ("idle2", 2.5f)
	};

	internal override void Configure()
	{
		base.Configure();
		PrimaryDelay = 0.15f;
		SecondaryDelay = 0.3f;
		ReloadTime = 2.2f;
		ClipMaxSize = 20;
		Ballistics = new() { Damage = 25, Range = 8192, Radius = 0 };
	}

	public override bool CanPrimaryAttack() => _burstRounds == 0 && base.CanPrimaryAttack();
	public override bool CanSecondaryAttack() => _burstRounds == 0 && base.CanSecondaryAttack();
	public override bool CanReload() => _burstRounds == 0 && base.CanReload();

	public override void SecondaryAttack() => BurstMode = !BurstMode;

	public override void PrimaryAttack()
	{
		if ( !FirePistolRound() ) return;
		SetNextFire( BurstMode ? 0.5f : PrimaryDelay );
		if ( !BurstMode || !IsHeld ) return;
		_burstRounds = 2;
		_nextBurstRound = 0.1f;
	}

	protected override void Think()
	{
		base.Think();
		if ( _burstRounds == 0 || _nextBurstRound > 0 ) return;
		if ( !IsHeld || IsReloading || !TakePrimaryAmmo() )
		{
			_burstRounds = 0;
			return;
		}

		--_burstRounds;
		_nextBurstRound = 0.1f;
		FireRound( 0.05f, rangeModifier: 0.9f );
	}

	protected override void OnHolstered()
	{
		_burstRounds = 0;
		base.OnHolstered();
	}
}

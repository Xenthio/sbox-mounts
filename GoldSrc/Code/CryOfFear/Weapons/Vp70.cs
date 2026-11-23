using GoldSrc.HalfLife;

namespace GoldSrc.CryOfFear;

[Title( "VP70" ), Category( "Cry of Fear" )]
[WeaponPickup( "vp70", "vp70", 180, Bucket = 4, Weight = 5, ModelDirectory = "models/weapons/vp70", Mount = "cof" )]
public sealed class Vp70 : MagazinePistol
{
	const float BurstInterval = 0.027f;
	int _roundsRemaining;
	TimeUntil _nextRound;

	internal override float TacticalReloadTime => 2.5f;
	internal override float EmptyReloadTime => 109f / 30;
	internal override float RecoilPerRound => 0.75f;
	internal override float AccuracyRecoveryDelay => 0.35f;
	internal override float DryFireDelay => 0.8f;
	internal override float FirstDrawTime => 104f / 30;
	internal override string WorldModelPath => "police/models/weapons/vp70/p_vp70.mdl";
	internal override string ShotSound => "weapons/vp70/vp70_fire.wav";
	internal override string DeploySequence => IsFirstDraw ? "draw_first" : "draw";
	internal override string ShotSequence => Zoomed ? "iron_shoot" : "shoot";
	internal override float? IdleAfterShot => 0.6f;
	internal override (string Name, float? Delay) IdleAnimation => Zoomed ? ("iron_idle", null) : ("idle", null);

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 18;
		PrimaryDelay = 0.55f;
		Ballistics = new() { Damage = 15, Range = 8192, Radius = 0 };
	}

	public override void PrimaryAttack()
	{
		if ( _roundsRemaining > 0 || !FireRound() ) return;
		_roundsRemaining = 2;
		_nextRound = BurstInterval;
		SetNextFire( PrimaryDelay );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( _roundsRemaining == 0 || _nextRound > 0 ) return;
		if ( IsProxy || !IsActive || !IsHeld || IsReloading || !FireRound() )
		{
			_roundsRemaining = 0;
			return;
		}

		--_roundsRemaining;
		_nextRound = BurstInterval;
	}

	protected override void OnHolstered()
	{
		_roundsRemaining = 0;
		base.OnHolstered();
	}
}

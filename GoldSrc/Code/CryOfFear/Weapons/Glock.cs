using GoldSrc.HalfLife;

namespace GoldSrc.CryOfFear;

[Title( "Glock 19" ), Category( "Cry of Fear" )]
[WeaponPickup( "glock", "glock", 150, Bucket = 4, Weight = 5, ModelDirectory = "models/weapons/glock", Mount = "cof" )]
public sealed class Glock : MagazinePistol
{
	internal override float TacticalReloadTime => 2.6f;
	internal override float EmptyReloadTime => 3.5f;
	internal override float RecoilPerRound => 1.25f;
	internal override float AccuracyRecoveryDelay => 0.55f;
	internal override float FirstDrawTime => 2.3f;
	internal override string WorldModelPath => "police/models/weapons/glock/p_glock.mdl";
	internal override string ShotSound => "weapons_new/glock/glock_fire.wav";
	internal override string DeploySequence => IsFirstDraw ? "draw_first" : Clip1 == 0 ? "draw_noshot" : "draw";
	internal override string RaiseSightsSequence => Clip1 == 0 ? "iron_to_noshot" : "iron_to";
	internal override string LowerSightsSequence => Clip1 == 0 ? "iron_from_noshot" : "iron_from";
	internal override string ShotSequence => Zoomed
		? (Clip1 == 0 ? "iron_shoot_empty" : "iron_shoot")
		: (Clip1 == 0 ? "shoot_empty_1" : "shoot_1");
	internal override (string Name, float? Delay) IdleAnimation => Zoomed
		? (Clip1 == 0 ? "iron_idle_noshot" : "iron_idle", null)
		: (Clip1 == 0 ? "idle1_noshot" : "idle1", null);
	internal override float? IdleAfterShot => 1;

	internal override void Configure()
	{
		base.Configure();
		PrimaryDelay = 0.1f;
		ClipMaxSize = 15;
		Ballistics = new() { Damage = 13, Range = 8192, Radius = 0 };
	}

	public override void PrimaryAttack()
	{
		if ( !FireRound() ) return;
		SetNextSecondaryFire( 1 );
		if ( Clip1 == 0 ) SetNextPrimaryFire( 1 );
	}
}

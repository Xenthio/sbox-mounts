using GoldSrc.HalfLife;

namespace GoldSrc.CryOfFear;

[Title( "Ruger P345" ), Category( "Cry of Fear" )]
[WeaponPickup( "p345", "p345", 80, Bucket = 4, Weight = 5, ModelDirectory = "models/weapons/p345", Mount = "cof" )]
public sealed class P345 : MagazinePistol
{
	internal override float TacticalReloadTime => 2.73f;
	internal override float EmptyReloadTime => 3.16f;
	internal override float RecoilPerRound => 0.85f;
	internal override float AccuracyRecoveryDelay => 0.75f;
	internal override string EmptySound => "weapons_new/p345/p345_click.wav";
	internal override string WorldModelPath => "police/models/weapons/p345/p_p345.mdl";
	internal override string ShotSound => "weapons_new/p345/p345_fire.wav";
	internal override string DeploySequence => Clip1 == 0 ? "draw_noshot" : "draw";
	internal override string RaiseSightsSequence => Clip1 == 0 ? "iron_to_noshot" : "iron_to";
	internal override string LowerSightsSequence => Clip1 == 0 ? "iron_from_noshot" : "iron_from";
	internal override string ShotSequence => Zoomed
		? (Clip1 == 0 ? "iron_shoot_empty" : "iron_shoot")
		: (Clip1 == 0 ? "shoot_empty_1" : "shoot_1");
	internal override float? IdleAfterShot => 1;
	internal override (string Name, float? Delay) IdleAnimation => Zoomed
		? (Clip1 == 0 ? "iron_idle_noshot" : "iron_idle", null)
		: (Clip1 == 0 ? "idle1_noshot" : "idletest", null);

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 8;
		PrimaryDelay = 0.2f;
		Ballistics = new() { Damage = 20, Range = 8192, Radius = 0 };
	}

	public override void PrimaryAttack()
	{
		if ( !FireRound() ) return;
		if ( Clip1 == 0 ) SetNextPrimaryFire( 1 );
		if ( !IsProxy && Owner is { ThirdPerson: false } )
			MeleeViewPunch.Apply( Scene.Camera, new Angles( -3, 0, Game.Random.Int( -4, 4 ) ) );
	}
}

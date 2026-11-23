using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Title( "M1 Garand" ), Category( "Day of Defeat" )]
[WeaponPickup( "garand", "ammo_16mm", 88, Bucket = 2, Weight = 20, Mount = "dod" )]
public sealed class Garand : DayOfDefeatRifle
{
	internal override int FreshPickupRounds => 88;
	internal override float StandingSpread => 0.016f;
	internal override int PlayerMuzzleFlash => 41;
	internal override string DeploySequence => Clip1 == 0 ? "draw_empty" : base.DeploySequence;
	internal override string MeleeSequence => Clip1 == 0 ? "smash_empty" : "smash";
	internal override string ShotSequence => Clip1 == 0 ? "shoot_empty" : $"shoot{Game.Random.Int( 1, 3 )}";

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 8;
		ReloadTime = 1.8f;
		Ballistics = Ballistics with { Damage = 120 };
	}

	public override bool CanReload() => Clip1 == 0 && base.CanReload();

	internal override void PlayShotSound()
	{
		base.PlayShotSound();
		if ( Clip1 == 0 ) PlaySample( "weapons/garand_reload_clipding.wav" );
	}
}

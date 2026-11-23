using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Title( "Kar98k" ), Category( "Day of Defeat" )]
[WeaponPickup( "98k", "ammo_16mm", 88, Bucket = 2, Weight = 20, Mount = "dod", ClassName = "kar" )]
public sealed class Kar98k : DayOfDefeatRifle
{
	internal override int FreshPickupRounds => 65;
	internal override float StandingSpread => 0.014f;
	internal override float MovingSpreadPenalty => 0.15f;
	internal override int PlayerMuzzleFlash => 41;
	internal override bool HasBayonet => true;
	internal override string MeleeSequence => "stab";
	internal override string WorldModelPath => "models/p_k98.mdl";
	internal override float BrassDelay => 27f / 30f;
	internal override (string Name, float? Delay) IdleAnimation => ("idle", 2.5f);

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 5;
		ReloadTime = 3.12f;
		PrimaryDelay = 1.6f;
		SecondaryDelay = 0.8f;
		Ballistics = Ballistics with { Damage = 160 };
	}

	internal override void PlayShotSound() => PlaySample( "weapons/kar_shoot.wav" );
}

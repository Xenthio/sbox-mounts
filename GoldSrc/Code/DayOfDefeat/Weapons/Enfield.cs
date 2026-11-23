using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Title( "Lee-Enfield" ), Category( "Day of Defeat" )]
[WeaponPickup( "enfield", "ammo_16mm", 60, Bucket = 2, Weight = 20, Mount = "dod" )]
public sealed class Enfield : DayOfDefeatRifle
{
	internal override int FreshPickupRounds => 60;
	internal override int PlayerMuzzleFlash => 41;
	internal override float StandingSpread => 0.014f;
	internal override float MovingSpreadPenalty => 0.15f;
	internal override float BrassDelay => 28f / 35f;
	internal override bool HasBayonet => true;
	internal override string MeleeSequence => "stab";
	internal override (string Name, float? Delay) IdleAnimation => ("idle", 2.5f);

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 5;
		DeployTime = 0.8f;
		PrimaryDelay = 1.65f;
		ReloadTime = 3.66f;
		Ballistics = Ballistics with { Damage = 160 };
	}
}

using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Title( "Scoped Kar98k" ), Category( "Day of Defeat" )]
[WeaponPickup( "scoped98k", "ammo_16mm", 65, Bucket = 2, Weight = 20, Mount = "dod", ClassName = "scopedkar" )]
public sealed class ScopedKar98k : ScopedRifle
{
	internal override string ScopeSprite => "scope_k43";
	internal override string WorldModelPath => "models/p_k98s.mdl";
	internal override string ShotSound => "weapons/kar_shoot.wav";
	internal override int FreshPickupRounds => 65;
	internal override float BrassDelay => 27f / 35f;
	internal override (string Name, float? Delay) IdleAnimation => ("idle", 2.5f);

	internal override void Configure()
	{
		base.Configure();
		PrimaryDelay = 1.61f;
		DeployTime = 1;
		ReloadTime = 3.12f;
		Ballistics = Ballistics with { Damage = 120 };
	}
}

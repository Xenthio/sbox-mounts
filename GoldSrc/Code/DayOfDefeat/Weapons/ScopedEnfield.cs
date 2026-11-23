using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Title( "Scoped Lee-Enfield" ), Category( "Day of Defeat" )]
[WeaponPickup( "enfield_scoped", "ammo_16mm", 60, Bucket = 2, Weight = 20, Mount = "dod", ClassName = "scopedenfield" )]
public sealed class ScopedEnfield : ScopedRifle
{
	internal override string ScopeSprite => "scope_enfield";
	internal override string WorldModelPath => "models/p_enfields.mdl";
	internal override string ShotSound => "weapons/enfieldsniper_shoot.wav";
	internal override int FreshPickupRounds => 60;
	internal override int BrassBodyGroup => 1;
	internal override float BrassDelay => 28f / 35f;
	internal override (string Name, float? Delay) IdleAnimation => ("idle", 2.5f);

	internal override void Configure()
	{
		base.Configure();
		PrimaryDelay = 1.65f;
		DeployTime = 0.8f;
		ReloadTime = 3.54f;
	}
}

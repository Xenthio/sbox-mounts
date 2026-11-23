using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Title( "Scoped FG42" ), Category( "Day of Defeat" )]
[WeaponPickup( "scopedfg42", "ammo_55mm", 180, Bucket = 2, Weight = 20, Mount = "dod", PickupModel = "w_fg42s" )]
public sealed class ScopedFg42 : ScopedRifle
{
	internal override string ScopeSprite => "scope_k43";
	internal override string WorldModelPath => "models/p_fg42s.mdl";
	internal override string ShotSound => "weapons/fg42_shoot.wav";
	internal override string DeploySequence => "up_draw";
	internal override string ReloadSequence => "up_reload";
	internal override string ShotSequence => "up_shoot";
	internal override (string Name, float? Delay) IdleAnimation => ("up_idle", 2.5f);
	internal override float StandingSpread => 0.025f;
	internal override float ScopedSpread => 0.008f;
	internal override bool CyclesBolt => false;
	internal override int PlayerMuzzleFlash => 61;

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 20;
		DeployTime = 0.3f;
		ReloadTime = 3.85f;
		PrimaryDelay = 0.085f;
		PrimaryAutomatic = true;
		Ballistics = Ballistics with { Damage = 85 };
	}

	public override void PrimaryAttack()
	{
		PrimaryDelay = Zoomed ? 0.3f : 0.085f;
		base.PrimaryAttack();
	}
}

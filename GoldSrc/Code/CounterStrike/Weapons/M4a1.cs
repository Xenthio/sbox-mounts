using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Title( "M4A1" ), Category( "Counter-Strike" )]
[WeaponPickup( "m4a1", "556nato", 90, Bucket = 0, Weight = 25, Mount = "cstrike" )]
public sealed class M4a1 : M4a1Weapon;

[Title( "M4A1" ), Category( "Counter-Strike: Condition Zero" )]
[WeaponPickup( "m4a1", "556nato", 90, Bucket = 0, Weight = 25, Mount = "czero" )]
public sealed class ConditionZeroM4a1 : M4a1Weapon;

[Hide]
public abstract class M4a1Weapon : CounterStrikeRifle
{
	[Sync] public bool Suppressed { get; private set; }
	internal override float AccuracyDivisor => 220;
	internal override float StandingSpread => Suppressed ? 0.025f : 0.02f;
	internal override float RangeModifier => Suppressed ? 0.95f : 0.97f;
	internal override float ShotDamage => Suppressed ? 33 : base.ShotDamage;
	internal override float? IdleAfterShot => 1.5f;
	internal override bool SupportsSecondary => true;

	internal override string DeploySequence => Suppressed ? "draw" : "draw_unsil";
	internal override string ReloadSequence => Suppressed ? "reload" : "reload_unsil";
	internal override string ShotSequence => base.ShotSequence + (Suppressed ? "" : "_unsil");
	internal override (string Name, float? Delay) IdleAnimation => (Suppressed ? "idle" : "idle_unsil", 20);
	internal override string ShotSound => Suppressed ? "weapons/m4a1-1.wav" : "weapons/m4a1_unsil-1.wav";

	public override bool CanReload() => NextSecondaryFire <= 0 && base.CanReload();

	public override void SecondaryAttack()
	{
		Suppressed = !Suppressed;
		SetNextFire( SecondaryDelay );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( Suppressed ? "add_silencer" : "detach_silencer", idleAfter: SecondaryDelay );
	}

	internal override void Configure()
	{
		base.Configure();
		PrimaryDelay = 0.0875f;
		SecondaryDelay = 2;
		ReloadTime = 3.05f;
		Ballistics = new() { Damage = 32, Range = 8192, Radius = 0 };
	}
}

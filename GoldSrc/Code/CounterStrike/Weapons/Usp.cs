using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Title( "USP" ), Category( "Counter-Strike" )]
[WeaponPickup( "usp", "45acp", 100, Bucket = 1, Weight = 5, Mount = "cstrike" )]
public sealed class Usp : UspWeapon;

[Title( "USP" ), Category( "Counter-Strike: Condition Zero" )]
[WeaponPickup( "usp", "45acp", 100, Bucket = 1, Weight = 5, Mount = "czero" )]
public sealed class ConditionZeroUsp : UspWeapon;

[Hide]
public abstract class UspWeapon : CounterStrikePistol
{
	[Sync] public bool Suppressed { get; private set; }

	internal override PistolAccuracy Accuracy => new( 0.92f, 0.6f, 0.3f, 0.275f );
	internal override float AirborneSpread => Suppressed ? 1.3f : 1.2f;
	internal override float MovingSpread => Suppressed ? 0.25f : 0.225f;
	internal override float CrouchingSpread => Suppressed ? 0.125f : 0.08f;
	internal override float StandingSpread => Suppressed ? 0.15f : 0.1f;
	internal override float RangeModifier => 0.79f;
	internal override float ShotDamage => Suppressed ? 30 : base.ShotDamage;
	internal override bool CanIdle => Clip1 > 0;

	string ModeSuffix => Suppressed ? "" : "_unsil";
	internal override string DeploySequence => "draw" + ModeSuffix;
	internal override string ReloadSequence => "reload" + ModeSuffix;
	internal override string ShotSequence => (Clip1 == 0 ? "shootlast" : $"shoot{Game.Random.Int( 1, 3 )}") + ModeSuffix;
	internal override (string Name, float? Delay) IdleAnimation => ("idle" + ModeSuffix, 60);
	internal override string ShotSound => Suppressed ? "weapons/usp1.wav" : "weapons/usp_unsil-1.wav";

	internal override void Configure()
	{
		base.Configure();
		PrimaryDelay = 0.15f;
		SecondaryDelay = 3;
		ReloadTime = 2.7f;
		ClipMaxSize = 12;
		Ballistics = new() { Damage = 34, Range = 4096, Radius = 0 };
	}

	public override void PrimaryAttack()
	{
		if ( FirePistolRound() ) SetNextFire( PrimaryDelay );
	}

	public override bool CanReload() => NextPrimaryFire <= 0 && base.CanReload();

	public override void SecondaryAttack()
	{
		Suppressed = !Suppressed;
		SetNextFire( SecondaryDelay );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( Suppressed ? "add_silencer" : "detach_silencer", idleAfter: SecondaryDelay );
	}

	public override Transform GetMuzzleTransform()
	{
		if ( !Suppressed && !IsProxy && Owner is { ThirdPerson: false } &&
			ViewModel?.GetComponent<SkinnedModelRenderer>()?.GetAttachment( "attachment3" ) is { } muzzle )
			return muzzle;
		return base.GetMuzzleTransform();
	}
}

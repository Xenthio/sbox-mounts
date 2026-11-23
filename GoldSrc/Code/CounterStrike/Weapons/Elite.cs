using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Title( "Dual Berettas" ), Category( "Counter-Strike" )]
[WeaponPickup( "elite", "9mm", 120, Bucket = 1, Weight = 5, Mount = "cstrike" )]
public sealed class Elite : EliteWeapon;

[Title( "Dual Berettas" ), Category( "Counter-Strike: Condition Zero" )]
[WeaponPickup( "elite", "9mm", 120, Bucket = 1, Weight = 5, Mount = "czero" )]
public sealed class ConditionZeroElite : EliteWeapon;

[Hide]
public abstract class EliteWeapon : CounterStrikePistol
{
	bool _nextIsLeft = true;
	bool _shotFromLeft;
	int _shotVariant;

	internal override PistolAccuracy Accuracy => new( 0.88f, 0.55f, 0.325f, 0.275f );
	internal override float AirborneSpread => 1.3f;
	internal override float MovingSpread => 0.175f;
	internal override float CrouchingSpread => 0.08f;
	internal override float StandingSpread => 0.1f;
	internal override float RangeModifier => 0.75f;
	internal override string ShotSound => "weapons/elite_fire.wav";
	internal override bool CanIdle => Clip1 > 0;
	internal override string ShotSequence =>
		$"shoot_{(_shotFromLeft ? "left" : "right")}{(Clip1 <= 1 ? "last" : _shotVariant.ToString())}";
	internal override (string Name, float? Delay) IdleAnimation => (Clip1 == 1 ? "idle_leftempty" : "idle", 60);

	public override Ray AimRay => Owner.IsValid() ? OffsetLaunchRay( new Vector3( 0, _shotFromLeft ? 5 : -5, 0 ) ) : base.AimRay;

	internal override void Configure()
	{
		base.Configure();
		Handedness = WeaponHandedness.Both;
		ClipMaxSize = 30;
		PrimaryDelay = 0.075f;
		ReloadTime = 4.5f;
		Ballistics = new() { Damage = 36, Range = 8192, Radius = 0 };
	}

	protected override void OnEquipped()
	{
		if ( (Clip1 & 1) == 0 ) _nextIsLeft = true;
		base.OnEquipped();
	}

	internal override void OnRoundFiring( float sinceLastShot )
	{
		_shotFromLeft = _nextIsLeft;
		_nextIsLeft = !_nextIsLeft;
		_shotVariant = sinceLastShot switch
		{
			>= 0.5f => 5,
			>= 0.4f => 4,
			>= 0.3f => 3,
			>= 0.2f => 2,
			_ => 1
		};
	}

	internal override void EjectPistolCasing()
	{
		var renderer = ViewModel?.GetComponent<SkinnedModelRenderer>();
		if ( Owner is not { ThirdPerson: false } owner ||
			renderer?.GetAttachment( _shotFromLeft ? "attachment3" : "attachment4" ) is not { } port )
		{
			base.EjectPistolCasing();
			return;
		}

		var outward = owner.EyeTransform.Rotation.Right * (_shotFromLeft ? -1 : 1);
		var velocity = owner.Velocity + outward * Game.Random.Float( 50, 70 ) + Vector3.Up * Game.Random.Float( 100, 150 );
		ShellCasing.Eject( port.Position, port.Rotation, false, velocity, ContentMount, "models/pshell.mdl" );
	}
}

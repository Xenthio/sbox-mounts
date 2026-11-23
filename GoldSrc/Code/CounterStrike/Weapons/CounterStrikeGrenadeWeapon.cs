using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Hide]
public abstract class CounterStrikeGrenadeWeapon : PinGrenadeWeapon
{
	internal override string DeploySequence => Definition.Mount == "czero" ? "draw" : "deploy";
	internal override string PinSequence => "pullpin";
	internal override string PinSound => "weapons/pinpull.wav";
	internal override float RedrawDelay => 0.75f;
	internal override (string Name, float? Delay) IdleAnimation => ("idle", Game.Random.Float( 10, 15 ));

	internal override void Configure()
	{
		base.Configure();
		StartingAmmo = 1;
	}

	public override bool CanSecondaryAttack() => false;

	internal sealed override string LaunchGrenade( Vector3 origin, Vector3 direction, float pitch, float pinAge )
	{
		var speed = System.Math.Clamp( (90 - pitch) * 6, 0, 750 );
		ThrowGrenade( origin, direction * speed + Owner.Velocity );
		return "throw";
	}

	internal abstract void ThrowGrenade( Vector3 origin, Vector3 velocity );
}

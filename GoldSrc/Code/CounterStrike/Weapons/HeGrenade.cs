using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Title( "HE Grenade" ), Category( "Counter-Strike" )]
[WeaponPickup( "hegrenade", "hegrenade", 1, Bucket = 3, Weight = 2, Mount = "cstrike" )]
public sealed class HeGrenade : HeGrenadeWeapon;

[Title( "HE Grenade" ), Category( "Counter-Strike: Condition Zero" )]
[WeaponPickup( "hegrenade", "hegrenade", 1, Bucket = 3, Weight = 2, Mount = "czero" )]
public sealed class ConditionZeroHeGrenade : HeGrenadeWeapon;

[Hide]
public abstract class HeGrenadeWeapon : PinGrenadeWeapon
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

	internal override string LaunchGrenade( Vector3 origin, Vector3 direction, float pitch, float pinAge )
	{
		var speed = System.Math.Clamp( (90 - pitch) * 6, 0, 750 );
		HeGrenadeProjectile.Throw( Owner.GameObject, origin, direction * speed + Owner.Velocity, ContentMount );
		return "throw";
	}
}

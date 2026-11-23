namespace GoldSrc.HalfLife;

[Title( "Hand Grenade" ), Category( "Half-Life" )]
[WeaponPickup( "grenade", "grenades", 10, ClassName = "handgrenade", Bucket = 4, Weight = 5 )]
public sealed class HandGrenade : PinGrenadeWeapon
{
	internal override (string Name, float? Delay) IdleAnimation => Game.Random.Float( 0, 1 ) <= 0.75f
		? ("idle", Game.Random.Float( 10, 15 )) : ("fidget", 2.5f);
	internal override string PinSequence => "pinpull";

	internal override void Configure()
	{
		base.Configure();
		StartingAmmo = 5;
	}

	internal override string LaunchGrenade( Vector3 origin, Vector3 direction, float pitch, float pinAge )
	{
		var speed = System.Math.Clamp( (90 - pitch) * 6.5f, 0, 1000 );
		TimedGrenade.Throw( Owner.GameObject, origin, direction * speed + Owner.Velocity, System.MathF.Max( 0, 3 - pinAge ) );
		return speed < 500 ? "throw1" : speed < 1000 ? "throw2" : "throw3";
	}
}

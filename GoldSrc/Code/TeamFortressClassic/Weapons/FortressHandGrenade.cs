using GoldSrc.HalfLife;

namespace GoldSrc.TeamFortressClassic;

[Hide]
public abstract class FortressHandGrenadeWeapon : PinGrenadeWeapon
{
	internal override float MinimumPinTime => 0.8f;
	internal override float HeldFuse => MinimumPinTime + 3;
	internal override string PinSequence => "pinpull";
	internal override string PinSound => "weapons/ax1.wav";
	internal override string WorldModelPath => "models/p_grenade.mdl";
	internal override (string Name, float? Delay) IdleAnimation => ("idle", null);

	internal static Vector3 HullCenter( PlayerController player ) =>
		player.WorldPosition + player.WorldRotation.Up * (player.CurrentHeight * 0.5f);

	internal override Vector3 LaunchOrigin( Vector3 direction ) => HullCenter( Owner );

	internal string Launch<T>( Vector3 origin, float pinAge, string name, string modelPath ) where T : FortressHandGrenade, new()
	{
		if ( ContentMount?.FindFile( modelPath ) is not { } model ) return "throw1";
		var fuse = HeldFuse - pinAge;
		var inHand = fuse <= 0;
		var aim = Owner.EyeAngles.ToRotation();
		var velocity = inHand ? Vector3.Zero : aim.Forward * 600
			+ aim.Up * Game.Random.Float( 190, 210 ) + aim.Right * Game.Random.Float( -10, 10 );

		TimedGrenade.Spawn<T>( Owner.GameObject, origin, velocity, fuse, name, $"mount://tfc/{model}.vmdl", grenade =>
		{
			grenade.InHand = inHand;
			if ( Owner.Body.IsValid() ) grenade.AddComponent<PhysicsFilter>().Body = Owner.Body.GameObject;
		} );
		return "throw1";
	}
}

[Hide]
public abstract class FortressHandGrenade : TimedGrenade
{
	internal bool InHand { get; set; }
	internal override float GravityScale => 0.81f;
	internal override float Friction => 0.6f;
	protected static GameMount Mount => Sandbox.Mounting.Directory.Get( "tfc" ) as GameMount;

	internal override void OnBounce( Rigidbody body ) =>
		Mount?.NpcAudio.Emit( "weapons/grenade_hit", WorldPosition, volume: 0.25f );
}

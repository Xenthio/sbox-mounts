using GoldSrc.HalfLife;

namespace GoldSrc.HalfLifeMMod;

[Hide]
public sealed class HornetPod : NpcProjectile
{
	internal override string HitTag => "acid";
	internal override float Lifetime => 10;

	internal override void Guide()
	{
		Velocity += Scene.PhysicsWorld.Gravity * (0.75f * Time.Delta);
		WorldRotation = Rotation.LookAt( Velocity );
	}

	internal override SceneTrace FlightTrace( Vector3 start, Vector3 end ) =>
		base.FlightTrace( start, end ).WithoutTags( "hornet", "playercontroller", "trigger" );

	internal override void OnImpact( IDamageable victim, Vector3 position, Vector3 normal )
	{
		var origin = position + normal;
		Blast.ApplyRadiusDamage( GameObject, Shooter, origin, Damage, Damage * 2.5f, false, HitTag, "blast" );
		ShowBurst( origin );
		if ( ModelMount is not { } mount || mount.FindMountPath( "models/hornet.mdl" ) is not { } path ) return;

		var model = $"mount://{mount.Ident}/{path}.vmdl";
		var spawn = position + normal * 25;
		for ( var i = 0; i < 8; ++i )
		{
			var yaw = i * 45 + Game.Random.Float( -22.5f, 22.5f );
			var velocity = Rotation.FromYaw( yaw ).Forward * 700 + Vector3.Up * Game.Random.Float( 210, 700 );
			NpcProjectile.Fire<SwarmHornet>( Shooter, model, 5, null, spawn, velocity.Normal, velocity.Length,
				hornet => hornet.TrackingTime = Game.Random.Float( 1.5f, 2.5f ) );
		}
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowBurst( Vector3 position )
	{
		var sample = $"debris/bustflesh{Game.Random.Int( 1, 2 )}.wav";
		ModelMount?.NpcAudio.Emit( sample, position, attenuation: 0.8f, pitch: Game.Random.Float( 1.1f, 1.2f ) );
	}
}

[Hide]
public sealed class SwarmHornet : Hornet
{
	internal float TrackingTime { get; set; }
	internal override float TrackingLifetime => TrackingTime;
	internal override float FlySpeed => 300;
	internal override float SeekDot => -1;
	internal override bool CanDash => false;
}

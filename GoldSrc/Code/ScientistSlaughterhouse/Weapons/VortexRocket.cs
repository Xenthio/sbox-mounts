using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

[Hide]
public abstract class VortexRocket : NpcProjectile
{
	SoundHandle _motor;
	bool _ignited, _deployed;
	internal abstract bool BlackHole { get; }
	internal virtual float IgnitionDelay => 0;
	internal override float Lifetime => 16;

	protected override void OnUpdate()
	{
		if ( Age < IgnitionDelay || Application.IsDedicatedServer || ModelMount is not { } mount ) return;
		if ( !_ignited )
		{
			_ignited = true;
			ProjectileTrail.Attach( GameObject, mount, "Vortex rocket trail", "lred", 4, 5, Color.White );
			_motor = NpcAudio.Play( mount.NpcAudio.Find( "weapons/rocket1.wav" ), WorldPosition );
		}
		if ( _motor.IsValid() ) _motor.Position = WorldPosition;
	}

	protected override void OnDisabled() => _motor?.Stop();

	internal override SceneTrace FlightTrace( Vector3 start, Vector3 end ) => base.FlightTrace( start, end ).WithoutTags( "trigger" );

	internal override void HandleContact( SceneTraceResult hit ) => DeployField( hit.EndPosition + hit.Normal * 2 );

	internal void DeployField( Vector3 position )
	{
		if ( _deployed ) return;
		_deployed = true;
		VortexField.Spawn( Shooter, position, Damage, BlackHole );
		GameObject.Destroy();
	}
}

[Hide]
public sealed class BlackHoleRocket : VortexRocket
{
	internal override bool BlackHole => true;
}

[Hide]
public sealed class SeekingVortexRocket : VortexRocket
{
	Vector3 _direction;
	TimeUntil _nextSeek;

	internal override bool BlackHole => false;
	internal override float IgnitionDelay => 0.4f;

	internal override void PrepareLaunch()
	{
		_direction = WorldRotation.Forward;
		_nextSeek = IgnitionDelay;
	}

	internal override SceneTrace FlightTrace( Vector3 start, Vector3 end ) => (Age >= IgnitionDelay
		? Scene.Trace.Box( BBox.FromPositionAndSize( Vector3.Zero, Vector3.One * 8 ), start, end )
		: Scene.Trace.Ray( start, end )).WithoutTags( "trigger" );

	internal override void Guide()
	{
		if ( Age < IgnitionDelay )
		{
			Velocity += Scene.PhysicsWorld.Gravity * (0.5f * Time.Delta);
			return;
		}
		if ( _nextSeek > 0 ) return;
		_nextSeek = 0.1f;
		if ( Target.IsValid() && !Sandbox.Npcs.BaseNpc.IsLiveTarget( Target ) ) Target = null;
		if ( !Target.IsValid() )
			Target = ProjectileTarget.Find( Scene, Shooter, new Ray( WorldPosition, _direction ), range: 512, minimumAlignment: 0 );

		_direction = Target.IsValid()
			? (Npc.TargetBodyPosition( Target ) - WorldPosition).Normal
			: _direction;
		if ( Velocity.Length < 300 ) Velocity = _direction * 512;
		var speed = Velocity.Length;
		var drive = speed < 800 ? System.MathF.Min( 1000, speed * 0.8f + 100 ) : speed * 0.8f;
		Velocity = (Velocity * 0.2f + _direction * drive).ClampLength( 1000 );
		WorldRotation = Rotation.LookAt( Velocity );
	}
}

namespace UnityMount;

readonly record struct UnityParticleVelocity( ParticleVector3 Linear, ParticleVector3 Orbital, bool WorldSpace );
readonly record struct UnityParticleSpeedRotation( ParticleVector3 AngularVelocity, Vector2 SpeedRange );

sealed record UnityParticleEmitter( bool PlayOnAwake, bool Loop, bool Prewarm, float TimeScale, float Duration, ParticleFloat Delay, int Limit, bool Local,
	UnityParticleShape Shape, ParticleFloat Lifetime, ParticleFloat Rate, ParticleFloat DistanceRate, UnityParticleBurst[] Bursts,
	ParticleFloat Speed, ParticleFloat Size, ParticleFloat? Height, ParticleFloat? Depth, ParticleFloat SizeOverLife,
	(ParticleFloat Height, ParticleFloat Depth)? AxisSizeOverLife, ParticleFloat Gravity, ParticleVector3 StartRotation,
	ParticleVector3 Spin, bool Rotation3D, Color Tint, Color? MinimumTint, ParticleGradient ColorOverLife, UnityParticleVelocity? Velocity,
	UnityParticleTrail Trail, UnityParticleCollision Collision, UnityParticleSpeedRotation? SpeedRotation, UnityParticleNoiseSettings Noise,
	UnityParticleSubEmitter[] CollisionEmitters )
{
	public const int ClassId = 198;
	public const int RendererClassId = 199;
	public static UnityParticleEmitter Read( UnityReader reader, UnityTypeTree tree )
	{
		var playOnAwake = ReadBool( tree, 0, "playOnAwake" );
		var shape = ReadModule( "ShapeModule", out var shapeModule, out var shapeStart )
			? UnityParticleShape.Read( reader, shapeModule, shapeStart ) : UnityParticleShape.Point;
		if ( shape is null ) return null;

		// A disabled emission module still permits manual emission into the particle system.
		var autoEmission = ReadModule( "EmissionModule", out var emission, out var emissionStart );
		var burstCount = autoEmission ? ReadInt( emission, emissionStart, "m_BurstCount" ) : 0;
		reader.Position = emissionStart;
		var bursts = burstCount == 0 ? [] : UnityParticleBurst.Read( reader, emission.SeekField( reader, "m_Bursts" ), burstCount );
		if ( !ReadModule( "InitialModule", out var initial, out var initialStart ) ) return null;
		var rotation3D = ReadBool( initial, initialStart, "rotation3D" );

		var space = ReadInt( tree, 0, "moveWithTransform" );
		if ( space is not (0 or 1) ) return null;

		var duration = ReadScalar( tree, 0, "lengthInSec" );
		var timeScale = ReadScalar( tree, 0, "simulationSpeed" );
		var limit = ReadInt( initial, initialStart, "maxNumParticles" );
		if ( duration <= 0 || timeScale <= 0 || limit is <= 0 or > 16384 ||
			!float.IsFinite( duration ) || !float.IsFinite( timeScale ) ) return null;

		var prewarm = ReadBool( tree, 0, "prewarm" );
		var height = ReadBool( initial, initialStart, "size3D" )
			? (ParticleFloat?)ReadFloat( initial, initialStart, "startSizeY", UnityCoordinates.ToInches( 1 ) ) : null;
		var depth = height.HasValue
			? (ParticleFloat?)ReadFloat( initial, initialStart, "startSizeZ", UnityCoordinates.ToInches( 1 ) ) : null;
		ParticleFloat sizeOverLife = 1;
		(ParticleFloat, ParticleFloat)? axisSizeOverLife = null;
		if ( ReadModule( "SizeModule", out var size, out var sizeStart ) )
		{
			sizeOverLife = ReadFloat( size, sizeStart, "curve" );
			if ( ReadBool( size, sizeStart, "separateAxes" ) )
				axisSizeOverLife = (ReadFloat( size, sizeStart, "y" ), ReadFloat( size, sizeStart, "z" ));
		}
		const float degrees = 180 / MathF.PI;
		var startRotation = new ParticleVector3 { Z = ReadFloat( initial, initialStart, "startRotation", degrees ) };
		if ( rotation3D )
		{
			startRotation.X = ReadFloat( initial, initialStart, "startRotationX", degrees );
			startRotation.Y = ReadFloat( initial, initialStart, "startRotationY", degrees );
		}
		var spin = new ParticleVector3();
		if ( ReadModule( "RotationModule", out var rotation, out var rotationStart ) )
		{
			spin.Z = ReadFloat( rotation, rotationStart, "curve", degrees );
			if ( rotation3D && ReadBool( rotation, rotationStart, "separateAxes" ) )
			{
				spin.X = ReadFloat( rotation, rotationStart, "x", degrees );
				spin.Y = ReadFloat( rotation, rotationStart, "y", degrees );
			}
		}
		UnityParticleVelocity? velocity = null;
		if ( ReadModule( "VelocityModule", out var motion, out var motionStart ) )
		{
			var units = UnityCoordinates.ToInches( 1 );
			var linear = new ParticleVector3
			{
				X = ReadFloat( motion, motionStart, "z", units ),
				Y = ReadFloat( motion, motionStart, "x", -units ),
				Z = ReadFloat( motion, motionStart, "y", units )
			};
			// Angular velocity reverses with the Unity-to-Source reflection.
			var orbital = new ParticleVector3
			{
				X = ReadFloat( motion, motionStart, "orbitalZ", -180 / MathF.PI ),
				Y = ReadFloat( motion, motionStart, "orbitalX", 180 / MathF.PI ),
				Z = ReadFloat( motion, motionStart, "orbitalY", -180 / MathF.PI )
			};
			velocity = new( linear, orbital, ReadBool( motion, motionStart, "inWorldSpace" ) );
		}

		reader.Position = initialStart;
		var startColor = initial.SeekField( reader, "startColor" );
		var colorStart = reader.Position;
		startColor.SeekField( reader, "minMaxState" );
		var startColorMode = reader.ReadInt16();
		if ( startColorMode is not (0 or 2) ) return null;
		reader.Position = colorStart;
		startColor.SeekField( reader, "maxColor" );
		var tint = UnityEffectValues.ReadColor( reader );
		Color? minimumTint = null;
		if ( startColorMode == 2 )
		{
			reader.Position = colorStart;
			startColor.SeekField( reader, "minColor" );
			minimumTint = UnityEffectValues.ReadColor( reader );
		}
		ParticleGradient color = Color.White;
		if ( ReadModule( "ColorModule", out var colorModule, out var colorModuleStart ) )
		{
			reader.Position = colorModuleStart;
			var gradient = colorModule.SeekField( reader, "gradient" );
			var gradientStart = reader.Position;
			gradient.SeekField( reader, "minMaxState" );
			if ( reader.ReadInt16() != 1 ) return null;

			reader.Position = gradientStart;
			color = new() { Type = ParticleGradient.ValueType.Gradient, GradientA = UnityEffectValues.ReadGradient( reader, gradient.SeekField( reader, "maxGradient" ) ) };
		}

		var trail = ReadModule( "TrailModule", out var trailModule, out var trailStart )
			? UnityParticleTrail.Read( reader, trailModule, trailStart ) : null;
		var collision = tree.HasField( "CollisionModule" ) && ReadModule( "CollisionModule", out var collisionModule, out var collisionStart )
			? UnityParticleCollision.Read( reader, collisionModule, collisionStart ) : null;
		var speedRotation = ReadSpeedRotation();
	var noise = tree.HasField( "NoiseModule" ) && ReadModule( "NoiseModule", out var noiseModule, out var noiseStart )
		? UnityParticleNoiseSettings.Read( reader, noiseModule, noiseStart ) : null;
		var collisionEmitters = collision is not null && tree.HasField( "SubModule" ) && ReadModule( "SubModule", out var subModule, out var subStart )
			? UnityParticleSubEmitter.Read( reader, subModule, subStart ) : [];
		return new( playOnAwake, ReadBool( tree, 0, "looping" ), prewarm, timeScale, duration, ReadFloat( tree, 0, "startDelay" ), limit,
			space == 0, shape, ReadFloat( initial, initialStart, "startLifetime", allowInfiniteLifetime: true ),
			autoEmission ? ReadFloat( emission, emissionStart, "rateOverTime" ) : 0f,
			autoEmission ? ReadFloat( emission, emissionStart, "rateOverDistance", 100 / UnityCoordinates.ToInches( 1 ) ) : 0f, bursts,
			ReadFloat( initial, initialStart, "startSpeed", UnityCoordinates.ToInches( 1 ) ),
			ReadFloat( initial, initialStart, "startSize", UnityCoordinates.ToInches( 1 ) ), height, depth, sizeOverLife, axisSizeOverLife,
			ReadFloat( initial, initialStart, "gravityModifier" ),
			startRotation, spin, rotation3D, tint, minimumTint, color, velocity, trail, collision, speedRotation, noise,
			collisionEmitters );

		UnityParticleSpeedRotation? ReadSpeedRotation()
		{
			if ( !tree.HasField( "RotationBySpeedModule" ) ||
				!ReadModule( "RotationBySpeedModule", out var module, out var start ) ) return null;

			reader.Position = start;
			module.SeekField( reader, "range" );
			var minimum = UnityCoordinates.ToInches( reader.ReadSingle() );
			var maximum = UnityCoordinates.ToInches( reader.ReadSingle() );
			if ( !float.IsFinite( minimum ) || !float.IsFinite( maximum ) || minimum < 0 || maximum < minimum )
				return null;

			var angular = new ParticleVector3 { Z = ReadFloat( module, start, "curve", degrees ) };
			if ( ReadBool( module, start, "separateAxes" ) )
			{
				// Meshes with 3D rotation keep Unity axes until rendering. Other effects use Source angles.
				angular.X = ReadFloat( module, start, "x", degrees );
				angular.Y = ReadFloat( module, start, "y", rotation3D ? degrees : -degrees );
			}
			return new( angular, new Vector2( minimum, maximum ) );
		}

		bool ReadModule( string field, out UnityTypeTree module, out long start )
		{
			reader.Position = 0;
			module = tree.SeekField( reader, field );
			start = reader.Position;
			return ReadBool( module, start, "enabled" );
		}

		bool ReadBool( UnityTypeTree layout, long offset, string field )
		{
			reader.Position = offset;
			layout.SeekField( reader, field );
			return reader.ReadBoolean();
		}

		int ReadInt( UnityTypeTree layout, long offset, string field )
		{
			reader.Position = offset;
			layout.SeekField( reader, field );
			return reader.ReadInt32();
		}

		float ReadScalar( UnityTypeTree layout, long offset, string field )
		{
			reader.Position = offset;
			layout.SeekField( reader, field );
			return reader.ReadSingle();
		}

		ParticleFloat ReadFloat( UnityTypeTree layout, long offset, string field, float multiplier = 1, bool allowInfiniteLifetime = false )
		{
			reader.Position = offset;
			return UnityEffectValues.ReadFloat( reader, layout.SeekField( reader, field ), multiplier, allowInfiniteLifetime );
		}
	}
}

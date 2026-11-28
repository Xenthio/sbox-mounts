using Sandbox.Utility;

namespace UnityMount;

/// <summary>Applies the authored turbulence of a mounted particle system.</summary>
[Title( "Mounted Particle Noise" )]
public sealed class UnityParticleNoise : Component, Component.ExecuteInEditor
{
	const string SampleKey = "UnityParticleNoiseSample";
	const float SampleDistance = .025f;
	static readonly INoiseField FieldX = Noise.PerlinField( new( Seed: 5633, Frequency: 1 ) );
	static readonly INoiseField FieldY = Noise.PerlinField( new( Seed: 7321, Frequency: 1 ) );
	static readonly INoiseField FieldZ = Noise.PerlinField( new( Seed: 8191, Frequency: 1 ) );

	[Property] public ParticleVector3 Strength { get; set; }
	[Property] public float Frequency { get; set; } = 1;
	[Property] public bool Damping { get; set; }
	[Property, Range( 0, 2 )] public int Quality { get; set; }
	[Property, Range( 1, 4 )] public int Octaves { get; set; } = 1;
	[Property] public float OctaveMultiplier { get; set; } = .5f;
	[Property] public float OctaveScale { get; set; } = 2;
	[Property] public ParticleFloat ScrollSpeed { get; set; }
	[Property] public bool RemapEnabled { get; set; }
	[Property] public ParticleVector3 Remap { get; set; }
	[Property] public ParticleFloat PositionAmount { get; set; } = 1;
	[Property] public ParticleFloat RotationAmount { get; set; }
	[Property] public ParticleFloat SizeAmount { get; set; }
	[Property] public bool LocalSpace { get; set; }
	[Property] public float Duration { get; set; }
	[Property] public bool Loop { get; set; }

	ParticleEffect _effect;
	Transform _origin;
	float _step;
	float _age;
	float _scroll;
	float _scrollSeed;

	protected override void OnEnabled()
	{
		_effect = GetComponent<ParticleEffect>( true );
		if ( !_effect.IsValid() ) return;
		_age = _scroll = 0;
		_scrollSeed = Game.Random.Float( 0, 1 );
		_effect.OnPreStep += BeginStep;
		_effect.OnStep += MoveParticle;
		_effect.OnPostStep += UpdateAppearance;
	}

	protected override void OnDisabled()
	{
		if ( _effect.IsValid() )
		{
			_effect.OnPreStep -= BeginStep;
			_effect.OnStep -= MoveParticle;
			_effect.OnPostStep -= UpdateAppearance;
		}
		_effect = null;
	}

	void BeginStep( float step )
	{
		_origin = WorldTransform;
		_step = step;
		var phase = Duration > 0 ? Math.Clamp( (_age + step * .5f) / Duration, 0, 1 ) : 0;
		_scroll += ScrollSpeed.Evaluate( phase, _scrollSeed ) * step;
		_age += step;
		if ( Loop && Duration > 0 ) _age %= Duration;
	}

	void MoveParticle( Particle particle, float life )
	{
		if ( _step <= 0 && SizeAmount.IsNearlyZero() ) return;
		var position = LocalSpace ? _origin.PointToLocal( particle.Position ) : particle.Position;
		var point = position / UnityCoordinates.ToInches( 1 ) + new Vector3( _scroll );
		var noise = SampleCurl( point );
		if ( RemapEnabled )
		{
			noise = new Vector3(
				Remap.X.Evaluate( ToCurveTime( noise.x ), particle.Rand( 193, 0 ) ),
				Remap.Y.Evaluate( ToCurveTime( noise.y ), particle.Rand( 197, 0 ) ),
				Remap.Z.Evaluate( ToCurveTime( noise.z ), particle.Rand( 199, 0 ) ) );
		}
		var strengthSeed = particle.Rand( 157, 0 );
		noise *= Strength.Evaluate( life, strengthSeed, strengthSeed, strengthSeed );

		var elapsed = _step * particle.TimeScale;
		var movement = UnityCoordinates.ToInches( noise ) * (PositionAmount.Evaluate( life, particle.Rand( 163, 0 ) ) * elapsed);
		particle.Position += LocalSpace ? _origin.Rotation * movement : movement;

		var spin = RotationAmount.Evaluate( life, particle.Rand( 167, 0 ) );
		var size = SizeAmount.Evaluate( life, particle.Rand( 173, 0 ) );
		if ( spin == 0 && size == 0 ) return;
		var sample = particle.Get<AppearanceSample>( SampleKey );
		if ( sample is null )
		{
			sample = new AppearanceSample();
			particle.Set( SampleKey, sample );
		}
		sample.Frame = particle.Frame;
		sample.Rotation = new Angles( noise.x, noise.y, noise.z ) * (spin * elapsed);
		sample.Scale = MathF.Max( 0, 1 + noise.z * size );
	}

	void UpdateAppearance( float step )
	{
		if ( RotationAmount.IsNearlyZero() && SizeAmount.IsNearlyZero() ) return;
		foreach ( var particle in _effect.Particles )
		{
			var sample = particle.Get<AppearanceSample>( SampleKey );
			if ( sample is null || sample.Frame != particle.Frame ) continue;
			particle.StartAngles += sample.Rotation;
			particle.Angles += sample.Rotation;
			if ( _effect.ApplyShape ) particle.Size *= sample.Scale;
		}
	}

	Vector3 SampleCurl( Vector3 point )
	{
		var frequency = Frequency;
		var weight = 1f;
		var curl = Vector3.Zero;
		for ( var octave = 0; octave < Octaves; octave++ )
		{
			var samplePoint = point * frequency;
			// Curl of a vector potential. Lower quality modes reuse gradient samples.
			var x = Gradient( FieldX, samplePoint );
			var y = Quality >= 1 ? Gradient( FieldY, samplePoint ) : x;
			var z = Quality >= 2 ? Gradient( FieldZ, samplePoint ) : x;
			var layer = new Vector3( z.y - y.z, x.z - z.x, y.x - x.y );
			curl += layer * (Damping ? weight / (1 + frequency) : weight);
			frequency *= OctaveScale;
			weight *= OctaveMultiplier;
		}
		return curl;
	}

	static Vector3 Gradient( INoiseField field, Vector3 point )
	{
		var dx = new Vector3( SampleDistance, 0, 0 );
		var dy = new Vector3( 0, SampleDistance, 0 );
		var dz = new Vector3( 0, 0, SampleDistance );
		return new Vector3(
			field.Sample( point + dx ) - field.Sample( point - dx ),
			field.Sample( point + dy ) - field.Sample( point - dy ),
			field.Sample( point + dz ) - field.Sample( point - dz ) ) / (2 * SampleDistance);
	}

	internal static float SizeMultiplier( Particle particle )
	{
		var sample = particle.Get<AppearanceSample>( SampleKey );
		return sample is not null && sample.Frame == particle.Frame ? sample.Scale : 1;
	}

	static float ToCurveTime( float value ) => Math.Clamp( value * .5f + .5f, 0, 1 );

	sealed class AppearanceSample
	{
		public int Frame;
		public Angles Rotation;
		public float Scale;
	}
}

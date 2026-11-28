namespace UnityMount;

/// <summary>Applies the authored linear and orbital velocity curves of a mounted particle system.</summary>
[Title( "Mounted Particle Motion" )]
public sealed class UnityParticleMotion : Component, Component.ExecuteInEditor
{
	[Property] public ParticleVector3 Linear { get; set; }
	[Property] public ParticleVector3 Orbital { get; set; }
	[Property] public bool WorldSpace { get; set; }

	ParticleEffect _effect;
	Transform _origin;
	float _step;

	protected override void OnEnabled()
	{
		_effect = GetComponent<ParticleEffect>( true );
		if ( !_effect.IsValid() ) return;
		_effect.OnPreStep += CaptureTransform;
		_effect.OnStep += MoveParticle;
	}

	protected override void OnDisabled()
	{
		if ( !_effect.IsValid() ) return;
		_effect.OnPreStep -= CaptureTransform;
		_effect.OnStep -= MoveParticle;
	}

	void CaptureTransform( float step )
	{
		_origin = WorldTransform;
		_step = step;
	}

	void MoveParticle( Particle particle, float life )
	{
		var linear = Linear.Evaluate( particle, 170 );
		var orbital = Orbital.Evaluate( particle, 221 );
		if ( !WorldSpace )
		{
			linear = _origin.Rotation * linear;
			orbital = _origin.Rotation * orbital;
		}
		if ( !orbital.IsNearlyZero() )
		{
			var rotation = Rotation.FromAxis( orbital.Normal, orbital.Length * _step );
			particle.Position = _origin.Position + rotation * (particle.Position - _origin.Position);
		}
		particle.Position += linear * _step;
	}
}

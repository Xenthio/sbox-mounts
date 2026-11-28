namespace UnityMount;

/// <summary>Applies the particle system's angular velocity curve at each particle's current speed.</summary>
[Title( "Mounted Particle Rotation by Speed" )]
public sealed class UnityParticleRotationBySpeed : Component, Component.ExecuteInEditor
{
	[Property] public ParticleVector3 AngularVelocity { get; set; }
	[Property] public Vector2 SpeedRange { get; set; }

	ParticleEffect _effect;

	protected override void OnEnabled()
	{
		_effect = GetComponent<ParticleEffect>( true );
		if ( _effect.IsValid() ) _effect.OnPostStep += RotateParticles;
	}

	protected override void OnDisabled()
	{
		if ( _effect.IsValid() ) _effect.OnPostStep -= RotateParticles;
		_effect = null;
	}

	void RotateParticles( float step )
	{
		if ( step <= 0 ) return;
		var span = SpeedRange.y - SpeedRange.x;
		foreach ( var particle in _effect.Particles )
		{
			var speed = span > 0 ? Math.Clamp( (particle.Velocity.Length - SpeedRange.x) / span, 0, 1 ) : 0;
			var elapsed = step * particle.TimeScale;
			var rotation = new Angles(
				AngularVelocity.X.Evaluate( speed, particle.Rand( 139, 0 ) ) * elapsed,
				AngularVelocity.Y.Evaluate( speed, particle.Rand( 149, 0 ) ) * elapsed,
				AngularVelocity.Z.Evaluate( speed, particle.Rand( 151, 0 ) ) * elapsed );
			// Keep the accumulated rotation when ParticleEffect evaluates its over-life angles on the next step.
			particle.StartAngles += rotation;
			particle.Angles += rotation;
		}
	}
}

namespace UnityMount;

/// <summary>Multiplies the color over life by a color chosen once per particle.</summary>
[Title( "Mounted Particle Start Color" )]
public sealed class UnityParticleStartColor : Component, Component.ExecuteInEditor
{
	[Property] public Color Minimum { get; set; } = Color.White;
	[Property] public Color Maximum { get; set; } = Color.White;

	ParticleEffect _effect;

	protected override void OnEnabled()
	{
		_effect = GetComponent<ParticleEffect>( true );
		if ( _effect.IsValid() ) _effect.OnStep += ApplyColor;
	}

	protected override void OnDisabled()
	{
		if ( _effect.IsValid() ) _effect.OnStep -= ApplyColor;
		_effect = null;
	}

	void ApplyColor( Particle particle, float life )
	{
		var start = Color.Lerp( Minimum, Maximum, particle.Rand( 113, 0 ) );
		var gradient = _effect.Gradient.Evaluate( life, particle.Rand( 107, 0 ) );
		var brightness = _effect.Brightness.Evaluate( particle, 109, 0 );
		particle.Color = start * gradient * _effect.Tint * new Color( brightness, 1 );
	}
}

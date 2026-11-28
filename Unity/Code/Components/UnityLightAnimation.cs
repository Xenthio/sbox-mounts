namespace UnityMount;

/// <summary>Plays a mounted prefab's authored light intensity curve.</summary>
[Title( "Mounted Light Animation" )]
public sealed class UnityLightAnimation : Component, Component.DontExecuteOnServer
{
	[Property] public PointLight Light { get; set; }
	[Property] public Curve Intensity { get; set; }
	[Property] public float PeakIntensity { get; set; }
	[Property] public float Duration { get; set; }
	[Property] public bool Loop { get; set; }
	[Property] public bool RandomStart { get; set; }

	float _time;
	float _ambientIntensity;
	float? _clipIntensity;

	internal void SetClipIntensity( float? intensity )
	{
		if ( intensity.HasValue && !_clipIntensity.HasValue && Light.IsValid() )
			_ambientIntensity = Light.Intensity;
		_clipIntensity = intensity;
		ApplyIntensity();
	}

	void ApplyIntensity()
	{
		if ( Light.IsValid() ) Light.Intensity = _clipIntensity ?? _ambientIntensity;
	}

	protected override void OnEnabled()
	{
		_time = RandomStart ? Game.Random.Float( 0, Duration ) : 0;
		_ambientIntensity = 0;
		ApplyIntensity();
	}

	protected override void OnUpdate()
	{
		if ( !Light.IsValid() || Duration <= 0 ) return;

		_time += Time.Delta;
		_ambientIntensity = Intensity.Evaluate( _time / Duration ) * PeakIntensity;
		ApplyIntensity();
		if ( Loop && _time >= Duration ) _time = 0;
	}

	protected override void OnDisabled()
	{
		_ambientIntensity = PeakIntensity;
		ApplyIntensity();
	}
}

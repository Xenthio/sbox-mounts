namespace UnityMount;

/// <summary>Plays intensity tracks from mounted Unity animation clips.</summary>
[Title( "Mounted Light Clip Player" )]
public sealed class UnityLightClipPlayer : Component, Component.DontExecuteOnServer
{
	public sealed record Track( Light Light, float[] Frames );
	public sealed record Clip( string Name, float Duration, float SampleRate, bool Looping, Track[] Tracks );

	[Property, Hide] public Clip[] Clips { get; set; } = [];
	public string CurrentClip => _clip?.Name;
	public float Position { get; private set; }
	public bool Playing { get; private set; }

	readonly Dictionary<Light, (float Intensity, UnityLightAnimation Ambient)> _lights = new();
	Clip _clip;

	public bool Play( string name )
	{
		var clip = Clips.FirstOrDefault( clip => clip.Name == name );
		if ( clip is null ) return false;

		Begin( clip );
		Playing = clip.Duration > 0;
		Seek( 0 );
		return true;
	}

	internal void Sample( Clip clip, float seconds )
	{
		if ( _clip != clip ) Begin( clip );
		Playing = false;
		Seek( clip.Looping && clip.Duration > 0 ? seconds % clip.Duration : seconds );
	}

	void Begin( Clip clip )
	{
		Stop();
		_clip = clip;
		foreach ( var track in clip.Tracks )
		{
			if ( !track.Light.IsValid() ) continue;
			var ambient = track.Light.GameObject.GetComponent<UnityLightAnimation>( true );
			_lights.TryAdd( track.Light, (track.Light.Intensity, ambient?.Light == track.Light ? ambient : null) );
		}
	}

	public void Seek( float seconds )
	{
		if ( _clip is null || !float.IsFinite( seconds ) ) return;

		Position = Math.Clamp( seconds, 0, _clip.Duration );
		foreach ( var track in _clip.Tracks )
		{
			if ( !track.Light.IsValid() || track.Frames.Length == 0 ) continue;

			var first = Math.Min( (int)(Position * _clip.SampleRate), track.Frames.Length - 1 );
			var next = Math.Min( first + 1, track.Frames.Length - 1 );
			var start = first / _clip.SampleRate;
			var end = Math.Min( next / _clip.SampleRate, _clip.Duration );
			var amount = end > start ? (Position - start) / (end - start) : 0;
			var intensity = MathX.Lerp( track.Frames[first], track.Frames[next], amount );
			if ( _lights[track.Light].Ambient is { } ambient && ambient.IsValid() )
				ambient.SetClipIntensity( intensity );
			else
				track.Light.Intensity = intensity;
		}
	}

	public void Stop()
	{
		foreach ( var (light, state) in _lights )
		{
			if ( state.Ambient.IsValid() ) state.Ambient.SetClipIntensity( null );
			else if ( light.IsValid() ) light.Intensity = state.Intensity;
		}
		_lights.Clear();
		_clip = null;
		Position = 0;
		Playing = false;
	}

	protected override void OnDisabled() => Stop();

	protected override void OnUpdate()
	{
		if ( !Playing ) return;
		var next = Position + Time.Delta;
		Seek( _clip.Looping ? next % _clip.Duration : next );
		if ( !_clip.Looping && next >= _clip.Duration ) Playing = false;
	}
}

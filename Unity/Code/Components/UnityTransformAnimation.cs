namespace UnityMount;

/// <summary>Plays mounted animation clips that target prefab transforms.</summary>
[Title( "Mounted Transform Animation" )]
public sealed class UnityTransformAnimation : Component
{
	public sealed record Track( GameObject Target, Transform Rest, Transform[] Frames );
	public sealed record Clip( string Name, float Duration, float SampleRate, bool Looping, Track[] Tracks );

	[Property, Hide] public Clip[] Clips { get; set; } = [];
	public string CurrentClip => _clip?.Name;
	public float Position { get; private set; }
	public bool Playing { get; private set; }

	Clip _clip;

	public bool Play( string name )
	{
		var clip = Clips.FirstOrDefault( clip => clip.Name == name );
		if ( clip is null ) return false;
		Stop();
		_clip = clip;
		Playing = clip.Duration > 0;
		Seek( 0 );
		return true;
	}

	public void Seek( float seconds )
	{
		if ( _clip is null || !float.IsFinite( seconds ) ) return;
		Position = Math.Clamp( seconds, 0, _clip.Duration );
		var sample = Position * _clip.SampleRate;
		foreach ( var track in _clip.Tracks )
		{
			if ( !track.Target.IsValid() || track.Frames.Length == 0 ) continue;
			var first = Math.Min( (int)sample, track.Frames.Length - 1 );
			var next = Math.Min( first + 1, track.Frames.Length - 1 );
			var start = first / _clip.SampleRate;
			var end = Math.Min( next / _clip.SampleRate, _clip.Duration );
			var fraction = end > start ? (Position - start) / (end - start) : 0;
			track.Target.LocalTransform = global::Transform.Lerp( track.Frames[first], track.Frames[next], fraction, clamp: true );
		}
	}

	public void Stop()
	{
		if ( _clip is null ) return;
		foreach ( var track in _clip.Tracks )
			if ( track.Target.IsValid() ) track.Target.LocalTransform = track.Rest;
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

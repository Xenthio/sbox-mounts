namespace UnityMount;

/// <summary>Authored Animator root motion sampled in s&amp;box model coordinates.</summary>
[Sandbox.ModelEditor.GameData( "unity_root_motion", AllowMultiple = true ), Hide]
public sealed class UnityRootMotionData
{
	public string Sequence { get; set; }
	public float Duration { get; set; }
	public float SampleRate { get; set; }
	public bool Looping { get; set; }
	public float CycleOffset { get; set; }
	public Transform[] Frames { get; set; } = [];

	/// <summary>Samples one clip relative to its first root pose, before any cycle offset.</summary>
	public Transform Sample( float seconds )
	{
		if ( Frames.Length == 0 || !float.IsFinite( seconds ) || SampleRate <= 0 ) return global::Transform.Zero;
		seconds = Math.Clamp( seconds, 0, Duration );
		var first = Math.Min( (int)(seconds * SampleRate), Frames.Length - 1 );
		var next = Math.Min( first + 1, Frames.Length - 1 );
		var start = first / SampleRate;
		var end = Math.Min( next / SampleRate, Duration );
		var fraction = end > start ? (seconds - start) / (end - start) : 0;
		return global::Transform.Lerp( Frames[first], Frames[next], fraction, clamp: true );
	}

	internal static UnityRootMotionData Bake( UnityAnimationClip clip )
	{
		if ( !clip.HasAnimatorRoot || clip.SampleAnimatorRoot( clip.TimeAtFrame( 0 ) ) is not { } first ) return null;
		var frames = new Transform[(int)MathF.Ceiling( clip.Duration * clip.Rate ) + 1];
		frames[0] = global::Transform.Zero;
		var moving = false;
		for ( var i = 1; i < frames.Length; i++ )
		{
			if ( clip.SampleAnimatorRoot( clip.TimeAtFrame( i ) ) is not { } pose ) return null;
			moving |= pose != first;
			frames[i] = first.ToLocal( pose );
		}
		if ( !moving ) return null;
		return new()
		{
			Sequence = clip.Name,
			Duration = clip.Duration,
			SampleRate = clip.Rate,
			Looping = clip.Looping,
			CycleOffset = clip.CycleOffset,
			Frames = frames
		};
	}
}

namespace UnityMount;

public sealed partial class UnityTimelinePlayer
{
	public sealed record ObjectTrack( ObjectCue[] Cues );
	public sealed record ObjectCue( double Start, double End, double ClipIn, double Speed, double HoldStart, double HoldEnd,
		float Duration, float SampleRate, bool Looping, UnityTransformAnimation.Track Pose, RendererVisibility[] Visibility );
	public sealed record RendererVisibility( Renderer Target, bool[] Frames );

	[Property, Hide] public ObjectTrack[] ObjectTracks { get; set; } = [];

	sealed record ObjectState( ObjectCue Cue, Transform? Transform, (Renderer Target, bool Enabled)[] Visibility );
	readonly Dictionary<ObjectTrack, ObjectState> _objectStates = new();

	void EvaluateObjects()
	{
		foreach ( var track in ObjectTracks )
		{
			var cue = track.Cues.FirstOrDefault( clip => Position >= clip.Start && Position < clip.End )
				?? track.Cues.FirstOrDefault( clip => (Position >= clip.HoldStart && Position < clip.Start) ||
					(Position >= clip.End && Position < clip.HoldEnd) );

			if ( _objectStates.GetValueOrDefault( track ) is { } previous && previous.Cue != cue )
			{
				RestoreObject( previous );
				_objectStates.Remove( track );
			}
			if ( cue is null ) continue;
			if ( !_objectStates.ContainsKey( track ) )
			{
				Transform? transform = cue.Pose?.Target.IsValid() == true ? cue.Pose.Target.LocalTransform : null;
				var visibility = cue.Visibility.Where( sample => sample.Target.IsValid() )
					.Select( sample => (sample.Target, sample.Target.Enabled) ).ToArray();
				_objectStates.Add( track, new( cue, transform, visibility ) );
			}

			var local = (float)((Math.Clamp( Position, cue.Start, cue.End ) - cue.Start) * cue.Speed + cue.ClipIn);
			local = cue.Looping && cue.Duration > 0 ? local % cue.Duration : Math.Clamp( local, 0, cue.Duration );
			var frame = (int)(local * cue.SampleRate);
			if ( cue.Pose is { Frames.Length: > 0 } pose && pose.Target.IsValid() )
			{
				var first = Math.Min( frame, pose.Frames.Length - 1 );
				var next = Math.Min( first + 1, pose.Frames.Length - 1 );
				var start = first / cue.SampleRate;
				var end = Math.Min( next / cue.SampleRate, cue.Duration );
				var fraction = end > start ? (local - start) / (end - start) : 0;
				pose.Target.LocalTransform = global::Transform.Lerp( pose.Frames[first], pose.Frames[next], fraction, clamp: true );
			}
			foreach ( var sample in cue.Visibility )
			{
				if ( !sample.Target.IsValid() || sample.Frames.Length == 0 ) continue;
				var enabled = sample.Frames[Math.Min( frame, sample.Frames.Length - 1 )];
				if ( sample.Target.Enabled != enabled ) sample.Target.Enabled = enabled;
			}
		}
	}

	void RestoreObjects()
	{
		foreach ( var state in _objectStates.Values ) RestoreObject( state );
		_objectStates.Clear();
	}

	static void RestoreObject( ObjectState state )
	{
		if ( state.Transform is { } transform && state.Cue.Pose.Target.IsValid() )
			state.Cue.Pose.Target.LocalTransform = transform;
		foreach ( var (renderer, enabled) in state.Visibility )
			if ( renderer.IsValid() ) renderer.Enabled = enabled;
	}
}

namespace UnityMount;

public sealed partial class UnityTimelinePlayer
{
	public sealed record SkeletalCue( double Start, double End, double ClipIn, double Speed, double HoldStart, double HoldEnd,
		string Sequence, bool Looping );
	public sealed record SkeletalTrack( SkinnedModelRenderer Renderer, SkeletalCue[] Cues );

	[Property, Hide] public SkeletalTrack[] SkeletalTracks { get; set; } = [];

	readonly record struct SequenceState( string Name, float Time, float Rate, bool Looping, bool Blending, bool UseAnimGraph );
	readonly Dictionary<SkinnedModelRenderer, SequenceState> _sequenceStates = new();

	void EvaluateSkeletons()
	{
		foreach ( var track in SkeletalTracks )
		{
			var renderer = track.Renderer;
			if ( !renderer.IsValid() ) continue;
			var cue = track.Cues.FirstOrDefault( clip => Position >= clip.Start && Position < clip.End )
				?? track.Cues.FirstOrDefault( clip => (Position >= clip.HoldStart && Position < clip.Start) ||
					(Position >= clip.End && Position < clip.HoldEnd) );
			if ( cue is null )
			{
				if ( _sequenceStates.Remove( renderer, out var previous ) ) RestoreSequence( renderer, previous );
				continue;
			}
			if ( !renderer.SceneModel.IsValid() ) continue;

			var sequence = renderer.Sequence;
			if ( !_sequenceStates.ContainsKey( renderer ) )
			{
				_sequenceStates.Add( renderer, new( sequence.Name, sequence.Time, sequence.PlaybackRate,
					sequence.Looping, sequence.Blending, renderer.UseAnimGraph ) );
				renderer.UseAnimGraph = false;
				sequence.Blending = false;
				sequence.Looping = false;
				sequence.PlaybackRate = 0;
			}
			if ( sequence.Name != cue.Sequence ) sequence.Name = cue.Sequence;
			var local = (float)((Math.Clamp( Position, cue.Start, cue.End ) - cue.Start) * cue.Speed + cue.ClipIn);
			var duration = sequence.Duration;
			sequence.TimeNormalized = duration > 0 ? (cue.Looping ? local % duration : Math.Clamp( local, 0, duration )) / duration : 0;
			renderer.SceneModel.Update( 0 );
		}
	}

	void RestoreSkeletons()
	{
		foreach ( var (renderer, state) in _sequenceStates )
			if ( renderer.IsValid() ) RestoreSequence( renderer, state );
		_sequenceStates.Clear();
	}

	static void RestoreSequence( SkinnedModelRenderer renderer, SequenceState state )
	{
		var sequence = renderer.Sequence;
		sequence.Blending = false;
		sequence.Name = state.Name;
		sequence.PlaybackRate = state.Rate;
		sequence.Looping = state.Looping;
		renderer.UseAnimGraph = state.UseAnimGraph;
		if ( renderer.SceneModel.IsValid() )
		{
			if ( !state.UseAnimGraph && sequence.Duration <= 0 ) renderer.SceneModel.UpdateToBindPose();
			else
			{
				if ( !state.UseAnimGraph ) sequence.Time = state.Time;
				renderer.SceneModel.Update( 0 );
			}
		}
		sequence.Blending = state.Blending;
	}
}

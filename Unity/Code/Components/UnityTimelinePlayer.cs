namespace UnityMount;

/// <summary>Plays supported tracks of a mounted Unity Timeline.</summary>
[Title( "Mounted Unity Timeline" )]
public sealed partial class UnityTimelinePlayer : Component
{
	public enum PostPlaybackState { Active, Inactive, Revert, LeaveAsIs }
	public enum DirectorWrapMode { Hold, Loop, None }
	public readonly record struct Cue( double Start, double End );
	public sealed record ActivationTrack( GameObject Target, PostPlaybackState EndState, Cue[] Cues );
	public sealed record LightCue( double Start, double End, double ClipIn, double Speed, double HoldStart, double HoldEnd,
		UnityLightClipPlayer.Clip Clip );
	public sealed record LightTrack( UnityLightClipPlayer Player, LightCue[] Cues );
	public sealed record MaterialCue( double Start, double End, double ClipIn, double Speed, double HoldStart, double HoldEnd,
		UnityMaterialAnimation.Clip Clip );
	public sealed record MaterialTrack( UnityMaterialAnimation Player, MaterialCue[] Cues );
	public sealed record CameraCue( double Start, double End, double ClipIn, double Speed, double HoldStart, double HoldEnd,
		float Duration, float SampleRate, bool Looping, float[] Rail, float[] Fov, float[] Enabled, Vector3[] Translation, bool RelativeTranslation );
	public sealed record CameraTrack( GameObject Target, CameraCue[] Cues );

	[Property] public string TimelineName { get; set; }
	[Property] public double Duration { get; set; }
	[Property] public double InitialTime { get; set; }
	[Property] public DirectorWrapMode WrapMode { get; set; } = DirectorWrapMode.None;
	[Property, Hide] public ActivationTrack[] ActivationTracks { get; set; } = [];
	[Property, Hide] public LightTrack[] LightTracks { get; set; } = [];
	[Property, Hide] public MaterialTrack[] MaterialTracks { get; set; } = [];
	[Property, Hide] public CameraTrack[] CameraTracks { get; set; } = [];

	public double Position { get; private set; }
	public bool Playing { get; private set; }

	readonly Dictionary<GameObject, bool> _initialStates = new();
	bool _hasPlaybackState;

	public UnityCameraShot CurrentCameraShot
	{
		get
		{
			if ( !_hasPlaybackState ) return null;

			UnityCameraShot selected = null;
			var earliestStart = double.PositiveInfinity;
			foreach ( var track in ActivationTracks )
			{
				if ( !track.Target.IsValid() || !track.Target.Enabled ||
					track.Target.GetComponent<UnityCameraShot>( true ) is not { Enabled: true } shot ) continue;
				if ( SampleCamera( track.Target ) is { } frame && frame.Cue.Enabled.Length > 0 &&
					frame.Cue.Enabled[frame.First] < .5f ) continue;
				foreach ( var cue in track.Cues )
				{
					if ( Position < cue.Start || Position >= cue.End || cue.Start >= earliestStart ) continue;
					earliestStart = cue.Start;
					selected = shot;
				}
			}
			return selected;
		}
	}

	public bool ApplyCameraShot( CameraComponent camera )
	{
		if ( !camera.IsValid() || CurrentCameraShot is not { } shot ) return false;
		GetCameraPose( shot ).Apply( camera );
		return true;
	}

	internal UnityCameraPose GetCameraPose( UnityCameraShot shot )
	{
		var rail = shot.RailPosition;
		var fov = shot.VerticalFieldOfView;
		Vector3? aim = null;
		if ( SampleCamera( shot.GameObject ) is { } frame )
		{
			var (cue, first, next, amount) = frame;
			if ( cue.Rail.Length > 0 ) rail = MathX.Lerp( cue.Rail[first], cue.Rail[next], amount );
			if ( cue.Fov.Length > 0 ) fov = MathX.Lerp( cue.Fov[first], cue.Fov[next], amount );
		}
		if ( shot.LookAtTarget.IsValid() && SampleCamera( shot.LookAtTarget ) is { } aimFrame )
		{
			var (cue, first, next, amount) = aimFrame;
			if ( cue.Translation.Length > 0 )
			{
				var local = Vector3.Lerp( cue.Translation[first], cue.Translation[next], amount );
				if ( cue.RelativeTranslation ) local += shot.LookAtTarget.LocalPosition - cue.Translation[0];
				aim = shot.LookAtTarget.Parent is { } parent ? parent.WorldTransform.PointToWorld( local ) : local;
			}
		}
		return shot.GetPose( rail, fov, aim );
	}

	(CameraCue Cue, int First, int Next, float Amount)? SampleCamera( GameObject target )
	{
		foreach ( var track in CameraTracks )
		{
			if ( track.Target != target ) continue;
			var cue = track.Cues.FirstOrDefault( clip => Position >= clip.Start && Position < clip.End )
				?? track.Cues.FirstOrDefault( clip => (Position >= clip.HoldStart && Position < clip.Start) ||
					(Position >= clip.End && Position < clip.HoldEnd) );
			if ( cue is null ) continue;

			var local = (float)((Math.Clamp( Position, cue.Start, cue.End ) - cue.Start) * cue.Speed + cue.ClipIn);
			local = cue.Looping && cue.Duration > 0 ? local % cue.Duration : Math.Clamp( local, 0, cue.Duration );
			var last = (int)MathF.Ceiling( cue.Duration * cue.SampleRate );
			var first = Math.Min( (int)(local * cue.SampleRate), last );
			var next = Math.Min( first + 1, last );
			var start = first / cue.SampleRate;
			var end = Math.Min( next / cue.SampleRate, cue.Duration );
			var amount = end > start ? Math.Clamp( (local - start) / (end - start), 0, 1 ) : 0;
			return (cue, first, next, amount);
		}
		return null;
	}

	public void Play()
	{
		Stop();
		StopSounds( includeTails: true );
		foreach ( var track in ActivationTracks )
			if ( track.Target.IsValid() ) _initialStates.TryAdd( track.Target, track.Target.Enabled );
		_hasPlaybackState = true;
		Playing = true;
		Evaluate( double.IsFinite( InitialTime ) ? InitialTime : 0 );
		AdvanceSounds( double.NegativeInfinity );
	}

	public void Seek( double seconds )
	{
		if ( !_hasPlaybackState || !double.IsFinite( seconds ) ) return;
		StopSounds( includeTails: true );
		Evaluate( seconds );
	}

	void Evaluate( double seconds )
	{
		Position = Math.Clamp( seconds, 0, Duration );
		foreach ( var track in ActivationTracks )
		{
			if ( !track.Target.IsValid() ) continue;
			var active = track.Cues.Any( cue => Position >= cue.Start && Position < cue.End );
			if ( track.Target.Enabled != active ) track.Target.Enabled = active;
		}
		EvaluateObjects();
		EvaluateSkeletons();
		foreach ( var track in LightTracks )
		{
			if ( !track.Player.IsValid() ) continue;
			var cue = track.Cues.FirstOrDefault( clip => Position >= clip.Start && Position < clip.End )
				?? track.Cues.FirstOrDefault( clip => (Position >= clip.HoldStart && Position < clip.Start) ||
					(Position >= clip.End && Position < clip.HoldEnd) );
			if ( cue is null )
			{
				track.Player.Stop();
				continue;
			}
			var local = Math.Clamp( Position, cue.Start, cue.End ) - cue.Start;
			track.Player.Sample( cue.Clip, (float)(local * cue.Speed + cue.ClipIn) );
		}
		foreach ( var track in MaterialTracks )
		{
			if ( !track.Player.IsValid() ) continue;
			var cue = track.Cues.FirstOrDefault( clip => Position >= clip.Start && Position < clip.End )
				?? track.Cues.FirstOrDefault( clip => (Position >= clip.HoldStart && Position < clip.Start) ||
					(Position >= clip.End && Position < clip.HoldEnd) );
			if ( cue is null )
			{
				track.Player.Stop();
				continue;
			}
			var local = Math.Clamp( Position, cue.Start, cue.End ) - cue.Start;
			track.Player.SampleAt( cue.Clip, (float)(local * cue.Speed + cue.ClipIn) );
		}
	}

	public void Stop()
	{
		if ( !_hasPlaybackState ) return;
		_hasPlaybackState = false;
		Playing = false;
		StopSounds( includeTails: false );
		RestoreObjects();
		RestoreSkeletons();
		foreach ( var track in MaterialTracks )
			if ( track.Player.IsValid() ) track.Player.Stop();
		foreach ( var track in LightTracks )
			if ( track.Player.IsValid() ) track.Player.Stop();
		foreach ( var track in ActivationTracks )
		{
			if ( !track.Target.IsValid() || !_initialStates.TryGetValue( track.Target, out var initial ) ) continue;
			track.Target.Enabled = track.EndState switch
			{
				PostPlaybackState.Active => true,
				PostPlaybackState.Inactive => false,
				PostPlaybackState.Revert => initial,
				_ => track.Target.Enabled
			};
		}
		_initialStates.Clear();
	}

	protected override void OnDisabled() => Stop();
	protected override void OnDestroy() => StopSounds( includeTails: true );

	protected override void OnUpdate()
	{
		if ( !Playing ) return;
		var previous = Position;
		var next = previous + Time.Delta;
		if ( WrapMode == DirectorWrapMode.Loop && Duration > 0 && next >= Duration )
		{
			Evaluate( Duration );
			AdvanceSounds( previous );
			StopSounds( includeTails: false );
			next %= Duration;
			previous = double.NegativeInfinity;
		}
		Evaluate( next );
		AdvanceSounds( previous );
		if ( next < Duration ) return;

		if ( WrapMode == DirectorWrapMode.Hold )
		{
			Playing = false;
			StopSounds( includeTails: false );
		}
		else Stop();
	}
}

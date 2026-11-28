namespace UnityMount;

static class UnityTimeline
{
	public const int DirectorClassId = 320;
	// Timeline's duration for clip-based assets is one discrete tick before the last clip end.
	public const double Tick = 1e-12;

	public sealed record Director( UnityReference Owner, UnityReference Asset, int WrapMode, double InitialTime,
		(UnityReference Track, UnityReference Target)[] Bindings );
	public sealed record Asset( string Name, double? FixedDuration, UnityReference[] Tracks );
	public sealed record Track( bool Muted, UnityReference[] Children, int? ActivationEnd, Cue[] Cues, AnimationCue[] Animation, InfiniteClip? Recording, Transform? TrackOffset );
	public readonly record struct InfiniteClip( UnityReference Clip, bool Hold );
	public readonly record struct Cue( double Start, double End, UnityReference Playable );
	public readonly record struct AnimationCue( Cue Window, double ClipIn, double Speed,
		double HoldStart, double HoldEnd );

	public static Director ReadDirector( UnityReader reader, UnityTypeTree tree )
	{
		if ( !UnitySceneObjects.ReadComponentEnabled( reader, tree ) ) return null;
		var owner = Reference( "m_GameObject" );
		var asset = Reference( "m_PlayableAsset" );
		reader.Position = 0;
		tree.SeekField( reader, "m_WrapMode" );
		var wrapMode = reader.ReadInt32();
		reader.Position = 0;
		tree.SeekField( reader, "m_InitialTime" );
		var initialTime = reader.ReadDouble();
		var bindings = new List<(UnityReference, UnityReference)>();
		reader.Position = 0;
		tree.SeekField( reader, "m_SceneBindings" ).VisitArray( reader, ( _, _ ) =>
			bindings.Add( (UnityReference.Read( reader ), UnityReference.Read( reader )) ) );
		return new( owner, asset, wrapMode, double.IsFinite( initialTime ) ? initialTime : 0, bindings.ToArray() );

		UnityReference Reference( string name )
		{
			reader.Position = 0;
			tree.SeekField( reader, name );
			return UnityReference.Read( reader );
		}
	}

	public static Asset ReadAsset( UnityReader reader, UnityTypeTree tree )
	{
		if ( !tree.HasField( "m_Tracks" ) || !tree.HasField( "m_DurationMode" ) ) return null;
		tree.SeekField( reader, "m_Name" );
		var name = reader.ReadAlignedString();
		reader.Position = 0;
		tree.SeekField( reader, "m_Tracks" );
		var tracks = UnityReference.ReadArray( reader );
		reader.Position = 0;
		tree.SeekField( reader, "m_DurationMode" );
		if ( reader.ReadInt32() != 1 ) return new( name, null, tracks );
		reader.Position = 0;
		tree.SeekField( reader, "m_FixedDuration" );
		var duration = reader.ReadDouble();
		return double.IsFinite( duration ) && duration >= 0 ? new( name, duration, tracks ) : null;
	}

	public static Track ReadTrack( UnityReader reader, UnityTypeTree tree )
	{
		if ( !tree.HasField( "m_Clips" ) || !tree.HasField( "m_Children" ) ) return null;
		tree.SeekField( reader, "m_Muted" );
		var muted = reader.ReadBoolean();
		reader.Position = 0;
		tree.SeekField( reader, "m_Children" );
		var children = UnityReference.ReadArray( reader );
		int? activationEnd = null;
		if ( tree.HasField( "m_PostPlaybackState" ) )
		{
			reader.Position = 0;
			tree.SeekField( reader, "m_PostPlaybackState" );
			activationEnd = reader.ReadInt32();
		}
		reader.Position = 0;
		var cues = new List<Cue>();
		var animation = new List<AnimationCue>();
		var animationTrack = tree.HasField( "m_InfiniteClip" ) && tree.HasField( "m_TrackOffset" );
		tree.SeekField( reader, "m_Clips" ).VisitArray( reader, ( clip, start ) =>
		{
			var time = ReadDouble( "m_Start" );
			var end = time + ReadDouble( "m_Duration" );
			if ( !double.IsFinite( time ) || !double.IsFinite( end ) || time < 0 || end <= time ) return;
			reader.Position = start;
			clip.SeekField( reader, "m_Asset" );
			var cue = new Cue( time, end, UnityReference.Read( reader ) );
			cues.Add( cue );
			if ( !animationTrack ) return;

			var clipIn = ReadDouble( "m_ClipIn" );
			var speed = ReadDouble( "m_TimeScale" );
			if ( !double.IsFinite( clipIn ) || !double.IsFinite( speed ) || clipIn < 0 || speed <= 0 ||
				ReadDouble( "m_EaseInDuration" ) > 0 || ReadDouble( "m_EaseOutDuration" ) > 0 ||
				ReadDouble( "m_BlendInDuration" ) > 0 || ReadDouble( "m_BlendOutDuration" ) > 0 ) return;

			var holdStart = ReadHold( "m_PreExtrapolationMode", "m_PreExtrapolationTime", time, -1 );
			var holdEnd = ReadHold( "m_PostExtrapolationMode", "m_PostExtrapolationTime", end, 1 );
			if ( holdStart is null || holdEnd is null ) return;
			animation.Add( new( cue, clipIn, speed, holdStart.Value, holdEnd.Value ) );

			double ReadDouble( string field )
			{
				reader.Position = start;
				clip.SeekField( reader, field );
				return reader.ReadDouble();
			}

			double? ReadHold( string modeField, string timeField, double boundary, int direction )
			{
				reader.Position = start;
				clip.SeekField( reader, modeField );
				var mode = reader.ReadInt32();
				var length = ReadDouble( timeField );
				if ( mode == 0 || length == 0 ) return boundary;
				if ( mode != 1 || double.IsNaN( length ) || length < 0 ) return null;
				return Math.Clamp( boundary + direction * length, 0, double.MaxValue );
			}
		} );
		InfiniteClip? recording = null;
		Transform? trackOffset = null;
		if ( animationTrack )
		{
			reader.Position = 0;
			tree.SeekField( reader, "m_TrackOffset" );
			// ApplyTransformOffsets uses the serialized transform as the track origin.
			if ( reader.ReadInt32() == 0 && tree.HasField( "m_Position" ) && tree.HasField( "m_EulerAngles" ) )
				trackOffset = ReadOffset( reader, tree );
			if ( cues.Count == 0 )
			{
				reader.Position = 0;
				tree.SeekField( reader, "m_InfiniteClip" );
				var reference = UnityReference.Read( reader );
				if ( reference.Id != 0 )
				{
					reader.Position = 0;
					tree.SeekField( reader, "m_InfiniteClipTimeOffset" );
					var timeOffset = reader.ReadDouble();
					reader.Position = 0;
					tree.SeekField( reader, "m_InfiniteClipPostExtrapolation" );
					var post = reader.ReadInt32();
					if ( timeOffset == 0 && post is 0 or 1 ) recording = new( reference, post == 1 );
				}
			}
		}
		return new( muted, children, activationEnd, cues.ToArray(), animation.ToArray(), recording, trackOffset );
	}

	public static (UnityReference Clip, int LoopMode, bool RemoveStartOffset, Transform Offset)? ReadAnimationPlayable( UnityReader reader, UnityTypeTree tree )
	{
		if ( !tree.HasField( "m_Clip" ) || !tree.HasField( "m_RemoveStartOffset" ) || !tree.HasField( "m_Loop" ) ) return null;
		tree.SeekField( reader, "m_Clip" );
		var clip = UnityReference.Read( reader );
		reader.Position = 0;
		tree.SeekField( reader, "m_RemoveStartOffset" );
		var removeStartOffset = reader.ReadBoolean();
		reader.Position = 0;
		tree.SeekField( reader, "m_Loop" );
		var loop = reader.ReadInt32();
		return (clip, loop, removeStartOffset, ReadOffset( reader, tree ));
	}

	static Transform ReadOffset( UnityReader reader, UnityTypeTree tree )
	{
		reader.Position = 0;
		tree.SeekField( reader, "m_Position" );
		var position = UnityCoordinates.Position( new( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() ) );
		reader.Position = 0;
		tree.SeekField( reader, "m_EulerAngles" );
		var rotation = UnityCoordinates.EulerRotation( new( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() ) );
		return new( position, rotation );
	}
}

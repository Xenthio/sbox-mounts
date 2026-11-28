namespace UnityMount;

sealed record UnitySceneTimeline( UnityAssetPointer Owner, string Name, double Duration, double InitialTime,
	UnityTimelinePlayer.DirectorWrapMode WrapMode, UnitySceneTimeline.Activation[] Tracks, UnitySceneTimeline.LightAnimation[] Lights,
	UnitySceneTimeline.CameraAnimation[] Cameras, UnitySceneTimeline.Sound[] Sounds, UnitySceneTimeline.SkeletalAnimation[] Skeletons,
	UnitySceneTimeline.MaterialAnimation[] Materials, UnitySceneTimeline.ObjectAnimation[] Objects )
{
	public sealed record Activation( UnityAssetPointer Target, UnityTimelinePlayer.PostPlaybackState EndState, UnityTimeline.Cue[] Cues );
	public sealed record LightAnimation( UnityAssetPointer Target, ClipCue[] Cues );
	public sealed record MaterialAnimation( UnityAssetPointer Target, ClipCue[] Cues );
	public sealed record ObjectAnimation( UnityAssetPointer Target, ObjectCue[] Cues );
	public sealed record ObjectCue( UnityTimeline.AnimationCue Timing, UnityAnimationClip Clip, bool Looping, Transform? RootOffset, bool RelativeRoot );
	public sealed record ClipCue( UnityTimeline.AnimationCue Timing, UnityAnimationClip Clip, bool Looping );
	public sealed record CameraAnimation( UnityAssetPointer Target, UnityTimelinePlayer.CameraCue[] Cues );
	public sealed record Sound( UnityAssetPointer Emitter, double Start, double End, string EventPath, bool StopAtEnd, long? OnceId );
	public sealed record SkeletalAnimation( UnityAssetPointer Target, SkeletalCue[] Cues );
	public sealed record SkeletalCue( UnityTimeline.AnimationCue Timing, UnityAnimationSource Source, string Name, bool Looping );

	public IEnumerable<UnityAssetPointer> ReferencedTransforms() =>
		Tracks.Select( track => track.Target ).Concat( Lights.Select( track => track.Target ) )
			.Concat( Cameras.Select( track => track.Target ) ).Concat( Sounds.Select( sound => sound.Emitter ) )
			.Concat( Skeletons.Select( track => track.Target ) ).Concat( Materials.Select( track => track.Target ) ).Concat( Objects.Select( track => track.Target ) ).Append( Owner );
}

sealed partial class UnityResourceRegistry
{
	UnitySceneTimeline[] ReadSceneTimelines( UnityAssetFile file, UnitySceneCamera[] cameras, UnitySceneMarkers markers )
	{
		var timelines = new List<UnitySceneTimeline>();
		foreach ( var asset in file.Data.Objects )
		{
			if ( asset.ClassId != UnityTimeline.DirectorClassId || asset.Tree is null ) continue;
			try
			{
				if ( ReadSceneTimeline( file, asset, cameras, markers ) is { } timeline ) timelines.Add( timeline );
			}
			catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
			{
				Log.Warning( $"Could not read Unity Timeline for director {asset.Id} in '{file.Entry.Name}': {e.Message}" );
			}
		}
		return timelines.ToArray();
	}

	UnitySceneTimeline ReadSceneTimeline( UnityAssetFile file, SerializedFile.Object asset, UnitySceneCamera[] sceneCameras,
		UnitySceneMarkers markers )
	{
		using var reader = ReadMetadata( file, asset );
		if ( UnityTimeline.ReadDirector( reader, asset.Tree ) is not { } director ||
			Resolve( file, director.Owner, UnitySceneObjects.GameObjectClassId ) is not { } ownerObject ||
			FindTransform( ownerObject ) is not { } owner ||
			Resolve( file, director.Asset, SerializedFile.MonoBehaviourClassId ) is not { } source ) return null;

		using var metadata = ReadMetadata( source.File, source.Asset );
		if ( UnityTimeline.ReadAsset( metadata, source.Asset.Tree ) is not { } timeline ) return null;

		var bindings = new Dictionary<UnityAssetPointer, UnityReference>();
		foreach ( var (track, target) in director.Bindings )
			if ( Resolve( file, track, SerializedFile.MonoBehaviourClassId ) is { } key )
				bindings.TryAdd( key, target );

		var duration = 0d;
		var visited = new HashSet<UnityAssetPointer>();
		var tracks = new List<UnitySceneTimeline.Activation>();
		var lights = new List<UnitySceneTimeline.LightAnimation>();
		var cameras = new List<UnitySceneTimeline.CameraAnimation>();
		var sounds = new List<UnitySceneTimeline.Sound>();
		var skeletons = new List<UnitySceneTimeline.SkeletalAnimation>();
		var materials = new List<UnitySceneTimeline.MaterialAnimation>();
		var objects = new List<UnitySceneTimeline.ObjectAnimation>();
		foreach ( var track in timeline.Tracks )
			VisitTrack( source.File, track, 0 );
		if ( tracks.Count == 0 && lights.Count == 0 && cameras.Count == 0 && sounds.Count == 0 && skeletons.Count == 0 && materials.Count == 0 && objects.Count == 0 )
			return null;

		var wrapMode = director.WrapMode is >= 0 and <= 2 ? (UnityTimelinePlayer.DirectorWrapMode)director.WrapMode
			: UnityTimelinePlayer.DirectorWrapMode.None;
		return new( owner, timeline.Name, timeline.FixedDuration ?? Math.Max( 0, duration - UnityTimeline.Tick ),
			director.InitialTime, wrapMode, tracks.ToArray(), lights.ToArray(), cameras.ToArray(),
			sounds.OrderBy( sound => sound.Start ).ToArray(), skeletons.ToArray(), materials.ToArray(), objects.ToArray() );

		void VisitTrack( UnityAssetFile parent, UnityReference reference, int depth )
		{
			if ( depth > 32 || Resolve( parent, reference, SerializedFile.MonoBehaviourClassId ) is not { } pointer ||
				!visited.Add( pointer ) ) return;
			using var trackReader = ReadMetadata( pointer.File, pointer.Asset );
			if ( UnityTimeline.ReadTrack( trackReader, pointer.Asset.Tree ) is not { Muted: false } track ) return;

			foreach ( var cue in track.Cues ) duration = Math.Max( duration, cue.End );
			var binding = bindings.GetValueOrDefault( pointer );
			if ( track.ActivationEnd is >= 0 and <= 3 && track.Cues.Length != 0 &&
				Resolve( file, binding, UnitySceneObjects.GameObjectClassId ) is { } gameObject &&
				FindTransform( gameObject ) is { } target && target != owner )
			{
				tracks.Add( new( target, (UnityTimelinePlayer.PostPlaybackState)track.ActivationEnd.Value, track.Cues ) );
				if ( markers.SoundTriggers?.TryGetValue( gameObject, out var events ) == true )
					AddActivationSounds( target, track.Cues, events );
			}
			else if ( track.Recording is not null || track.Animation.Length > 0 && track.Animation.Length == track.Cues.Length )
				ReadAnimationTrack( pointer.File, binding, track );
			else if ( track.ActivationEnd is null && markers.SoundEvents is { Count: > 0 } )
				ReadSoundTrack( pointer.File, binding, track.Cues );
			foreach ( var child in track.Children )
				VisitTrack( pointer.File, child, depth + 1 );
		}

		void AddActivationSounds( UnityAssetPointer target, UnityTimeline.Cue[] cues, List<UnityWwiseCues.Trigger> events )
		{
			var ordered = cues.OrderBy( cue => cue.Start ).ToArray();
			var start = ordered[0].Start;
			var end = ordered[0].End;
			foreach ( var cue in ordered.Skip( 1 ) )
			{
				if ( cue.Start > end )
				{
					AddWindow();
					start = cue.Start;
				}
				end = Math.Max( end, cue.End );
			}
			AddWindow();

			void AddWindow()
			{
				foreach ( var entry in events )
					sounds.Add( new( target, start, end, markers.SoundEvents[entry.EventId], false, entry.OnceId ) );
			}
		}

		void ReadSoundTrack( UnityAssetFile trackFile, UnityReference binding, UnityTimeline.Cue[] cues )
		{
			foreach ( var cue in cues )
			{
				if ( Resolve( trackFile, cue.Playable, SerializedFile.MonoBehaviourClassId ) is not { } playable ) continue;
				using var content = ReadMetadata( playable.File, playable.Asset );
				if ( UnityWwiseCues.ReadTimeline( bundles, playable.File, content, playable.Asset.Tree ) is not { } sound ||
					!markers.SoundEvents.TryGetValue( sound.EventId, out var path ) ) continue;
				var emitter = Resolve( file, binding, UnitySceneObjects.GameObjectClassId ) is { } boundObject
					? FindTransform( boundObject ) ?? owner : owner;
				sounds.Add( new( emitter, cue.Start, cue.End, path, sound.StopAtEnd, null ) );
			}
		}

		void ReadAnimationTrack( UnityAssetFile trackFile, UnityReference binding, UnityTimeline.Track track )
		{
			if ( Resolve( file, binding, UnityAvatar.AnimatorClassId ) is not { } animator ) return;
			using var animatorReader = ReadMetadata( animator.File, animator.Asset );
			animator.Asset.Tree.SeekField( animatorReader, "m_GameObject" );
			if ( Resolve( animator.File, UnityReference.Read( animatorReader ), UnitySceneObjects.GameObjectClassId ) is not { } body ||
				FindTransform( body ) is not { } target ) return;

			animatorReader.Position = 0;
			animator.Asset.Tree.SeekField( animatorReader, "m_Avatar" );
			var rigid = UnityReference.Read( animatorReader ).Id == 0;
			var objectCues = new List<UnitySceneTimeline.ObjectCue>();
			var lightCues = new List<UnitySceneTimeline.ClipCue>();
			var materialCues = new List<UnitySceneTimeline.ClipCue>();
			var cameraCues = new List<UnityTimelinePlayer.CameraCue>();
			var skeletalCues = new List<UnitySceneTimeline.SkeletalCue>();
			var shot = sceneCameras.FirstOrDefault( camera => camera.Transform == target );
			var aim = sceneCameras.Any( camera => camera.LookAt == target );
			if ( track.Recording is { } infinite &&
				Resolve( trackFile, infinite.Clip, UnityAnimationClip.ClassId ) is { } recording &&
				recording.File.ReadAnimation( recording.Asset ) is { Duration: > 0 } recorded &&
				recorded.VisibilityBindings.Count > 0 )
			{
				duration = Math.Max( duration, recorded.Duration );
				var end = infinite.Hold ? double.MaxValue : recorded.Duration;
				objectCues.Add( new( new( new( 0, recorded.Duration, default ), 0, 1, 0, end ), recorded, false, null, false ) );
			}
			var previousEnd = 0d;
			foreach ( var cue in track.Animation.OrderBy( clip => clip.Window.Start ) )
			{
				if ( cue.Window.Start < previousEnd ) return;
				previousEnd = cue.Window.End;
				if ( Resolve( trackFile, cue.Window.Playable, SerializedFile.MonoBehaviourClassId ) is not { } playable ) continue;
				using var clipReader = ReadMetadata( playable.File, playable.Asset );
				if ( UnityTimeline.ReadAnimationPlayable( clipReader, playable.Asset.Tree ) is not { } assetReference ||
					Resolve( playable.File, assetReference.Clip, UnityAnimationClip.ClassId ) is not { } animationAsset ||
					animationAsset.File.ReadAnimation( animationAsset.Asset ) is not { } clip ) continue;
				var looping = assetReference.LoopMode == 1 || (assetReference.LoopMode == 0 && clip.Looping);
				if ( clip.LightBindings.Count > 0 ) lightCues.Add( new( cue, clip, looping ) );
				if ( clip.MaterialBindings.Count > 0 ) materialCues.Add( new( cue, clip, looping ) );
				if ( clip.HasTransformBindings ) skeletalCues.Add( new( cue, new( animationAsset.File, animationAsset.Asset ), clip.Name, looping ) );
				Transform? rootOffset = rigid && shot is null && !aim && track.TrackOffset is { } offset && clip.RootBindings.Count > 0
					? offset.ToWorld( assetReference.Offset ) : null;
				if ( rootOffset is not null || clip.VisibilityBindings.Count > 0 )
					objectCues.Add( new( cue, clip, looping, rootOffset, assetReference.RemoveStartOffset ) );
				if ( shot is null && !aim ) continue;
				var rail = shot?.Rail.Length > 1 ? CameraFrames( UnityAnimationClip.SplineProgress, "PositionAlongBasicBezierSpline" ) : [];
				var fov = shot is not null ? CameraFrames( UnityAnimationClip.CameraFieldOfView, "ForcedCamera" ) : [];
				var enabled = shot is not null ? CameraFrames( UnityAnimationClip.CameraEnabled, "ForcedCamera" ) : [];
				var translations = aim && clip.RootTranslation is { } channel ? SampleTranslation( clip, channel ) : [];
				if ( rail.Length + fov.Length + enabled.Length + translations.Length == 0 ) continue;
				cameraCues.Add( new( cue.Window.Start, cue.Window.End, cue.ClipIn, cue.Speed, cue.HoldStart, cue.HoldEnd,
					clip.Duration, clip.Rate, looping, rail, fov, enabled, translations, assetReference.RemoveStartOffset ) );

				float[] CameraFrames( uint attribute, string className )
				{
					foreach ( var scalar in clip.ScriptBindings )
					{
						if ( scalar.Attribute != attribute ||
							Resolve( animationAsset.File, scalar.Script, SerializedFile.MonoScriptClassId ) is not { } script ) continue;
						using var descriptor = ReadMetadata( script.File, script.Asset );
						script.Asset.Tree.SeekField( descriptor, "m_ClassName" );
						if ( descriptor.ReadAlignedString() == className ) return clip.SampleScalarFrames( scalar.Channel );
					}
					return [];
				}
			}
			if ( objectCues.Count > 0 ) objects.Add( new( target, objectCues.ToArray() ) );
			if ( lightCues.Count > 0 ) lights.Add( new( target, lightCues.ToArray() ) );
			if ( materialCues.Count > 0 ) materials.Add( new( target, materialCues.ToArray() ) );
			if ( cameraCues.Count > 0 ) cameras.Add( new( target, cameraCues.ToArray() ) );
			if ( skeletalCues.Count > 0 ) skeletons.Add( new( target, skeletalCues.ToArray() ) );
		}

		UnityAssetPointer? Resolve( UnityAssetFile parent, UnityReference reference, int classId ) =>
			bundles.TryResolve( parent, reference, classId, out var resolved, out var result ) && result.Tree is not null
				? new( resolved, result ) : null;
	}

	static Vector3[] SampleTranslation( UnityAnimationClip clip, int channel )
	{
		var frames = new Vector3[(int)MathF.Ceiling( clip.Duration * clip.Rate ) + 1];
		for ( var i = 0; i < frames.Length; i++ )
			frames[i] = UnityCoordinates.Position( clip.SampleVector( channel, clip.TimeAtFrame( i ) ) );
		return frames;
	}
}

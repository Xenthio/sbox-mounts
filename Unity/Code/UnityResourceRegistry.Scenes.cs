namespace UnityMount;

sealed record UnitySceneMarkers( HashSet<UnityAssetPointer> SpawnPoints, HashSet<UnityAssetPointer> InactiveAtStart,
	Dictionary<UnityAssetPointer, UnitySurfaceDefinition> Surfaces,
	IReadOnlyDictionary<string, UnityResourceDefinition> Resources = null,
	IReadOnlyDictionary<UnityAssetPointer, UnityAuthoredCamera> Cameras = null,
	IReadOnlyDictionary<UnityAssetPointer, List<UnityWwiseCues.Trigger>> SoundTriggers = null,
	IReadOnlyDictionary<uint, string> SoundEvents = null,
	IReadOnlyDictionary<string, string> PrefabExports = null );

sealed partial class UnityResourceRegistry
{
	sealed record SceneLayout( UnitySceneMarkers Markers )
	{
		public HashSet<UnityAssetPointer> ParticlePlanes { get; } = new();
	}

	public static IEnumerable<(string Name, string Archive)> FindScenes( UnityAssetFile manifest )
	{
		foreach ( var bundle in manifest.Data.Objects.Where( asset => asset.ClassId == UnitySceneObjects.AssetBundleClassId && asset.Tree is not null ) )
		{
			using var reader = ReadMetadata( manifest, bundle );
			foreach ( var scene in UnitySceneObjects.ReadSceneArchives( reader, bundle.Tree ) )
			{
				var name = Path.GetFileNameWithoutExtension( scene.Path );
				if ( IsResourceName( name ) ) yield return (name, scene.Archive);
			}
		}
	}

	public UnitySceneContent ReadScene( UnityAssetFile file, UnitySceneMarkers markers )
	{
		var layout = new SceneLayout( markers );
		var cameras = ReadSceneCameras( markers.Cameras );
		var timelines = ReadSceneTimelines( file, cameras, markers );
		var retained = timelines.SelectMany( timeline => timeline.ReferencedTransforms() ).ToHashSet();
		foreach ( var camera in cameras )
		{
			retained.Add( camera.Transform );
			if ( camera.LookAt is { } target ) retained.Add( target );
		}
		var staticBatches = new UnityStaticBatchCache();
		var timelineClips = timelines.SelectMany( timeline => timeline.Skeletons ).GroupBy( track => track.Target )
			.ToDictionary( group => group.Key, group => group.SelectMany( track => track.Cues ).Select( cue => cue.Source ).Distinct().ToArray() );
		RegisterModels( file, staticBatches, timelineClips );
		var roots = new List<UnityPrefabNode>();
		foreach ( var asset in file.Data.Objects.Where( asset => asset.ClassId == UnitySceneObjects.TransformClassId && asset.Tree is not null ) )
		{
			if ( ReadTransform( file, asset ).Parent.Id != 0 ) continue;
			roots.Add( ReadPrefab( file, asset, new(), 0, scene: layout ) );
		}
		retained.UnionWith( layout.ParticlePlanes );
		var visibleRoots = roots.Select( root => PruneSceneNode( root, retained ) ).OfType<UnityPrefabNode>().ToArray();
		if ( visibleRoots.Length == 0 ) throw new InvalidDataException( $"Unity scene '{file.Entry.Name}' contains no supported objects." );

		RegisterScenePrefabExports( visibleRoots, markers.PrefabExports );

		var settings = ReadRenderSettings( file );
		var sky = RegisterSceneSky( file, settings.Skybox );
		var reflection = settings.EnvironmentReflection is { } probe ? RegisterEnvironmentReflection( file, probe ) : null;
		return new( visibleRoots, sky, settings.Fog, reflection, settings.FlatAmbient, _resources, staticBatches, timelines, cameras );
	}

	void RegisterScenePrefabExports( UnityPrefabNode[] roots, IReadOnlyDictionary<string, string> exports )
	{
		if ( exports is null || exports.Count == 0 ) return;
		foreach ( var (name, path) in exports )
		{
			var source = Find( roots, name );
			if ( source is null ) continue;
			RegisterPrefab( path, source.Source.Asset.Id, source with { Local = Transform.Zero, Enabled = true } );
		}

		static UnityPrefabNode Find( IEnumerable<UnityPrefabNode> nodes, string name )
		{
			foreach ( var node in nodes )
			{
				if ( node.Name == name ) return node;
				if ( Find( node.Children, name ) is { } child ) return child;
			}
			return null;
		}
	}

	UnitySceneCamera[] ReadSceneCameras( IReadOnlyDictionary<UnityAssetPointer, UnityAuthoredCamera> cameras )
	{
		if ( cameras is null || cameras.Count == 0 ) return [];
		var transforms = new Dictionary<UnityAssetPointer, UnityAssetPointer>();
		foreach ( var (owner, camera) in cameras )
			if ( FindTransform( owner ) is { } transform ) transforms.TryAdd( camera.Component, transform );

		var result = new List<UnitySceneCamera>();
		foreach ( var camera in cameras.Values )
		{
			if ( !transforms.TryGetValue( camera.Component, out var transform ) ) continue;
			UnityAssetPointer? next = camera.NextComponent is { } component && transforms.TryGetValue( component, out var target )
				? target : null;
			result.Add( new( transform, camera.FieldOfView, camera.EntryTime, camera.ExitTime, next, camera.LookAt,
				BuildCameraRail( camera.Rail ), camera.Rail?.Position ?? 0 ) );
		}
		return result.ToArray();
	}

	UnityCameraShot.RailSample[] BuildCameraRail( UnityCameraRail rail )
	{
		if ( rail is null ) return [];
		var frames = new Transform[rail.Controls.Length];
		for ( var i = 0; i < frames.Length; i++ )
		{
			if ( ReadWorldTransform( rail.Controls[i].Transform ) is not { } world ) return [];
			frames[i] = world;
		}

		var samples = new List<UnityCameraShot.RailSample> { new( frames[0].Position, 0 ) };
		var distance = 0f;
		for ( var segment = 0; segment < frames.Length - 1; segment++ )
		{
			var from = frames[segment];
			var to = frames[segment + 1];
			var a = from.Position;
			var b = a + from.Rotation * rail.Controls[segment].Forward;
			var d = to.Position;
			var c = d + to.Rotation * rail.Controls[segment + 1].Backward;
			for ( var i = 1; i < rail.SamplesPerSegment; i++ )
			{
				var t = i / (float)(rail.SamplesPerSegment - 1);
				var p = Vector3.Lerp( Vector3.Lerp( a, b, t ), Vector3.Lerp( b, c, t ), t );
				var q = Vector3.Lerp( Vector3.Lerp( b, c, t ), Vector3.Lerp( c, d, t ), t );
				var position = Vector3.Lerp( p, q, t );
				distance += samples[^1].Position.Distance( position );
				samples.Add( new( position, distance ) );
			}
		}
		return float.IsFinite( distance ) && distance > 0 ? samples.ToArray() : [];
	}

	static UnityRenderSettings ReadRenderSettings( UnityAssetFile file )
	{
		var settings = file.Data.Objects.FirstOrDefault( asset => asset.ClassId == UnityRenderSettings.ClassId && asset.Tree is not null );
		if ( settings.Tree is null ) return default;
		try
		{
			using var reader = ReadMetadata( file, settings );
			return UnityRenderSettings.Read( reader, settings.Tree );
		}
		catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
		{
			Log.Warning( $"Could not read Unity render settings in '{file.Entry.Name}': {e.Message}" );
			return default;
		}
	}

	static UnityPrefabNode PruneSceneNode( UnityPrefabNode node, HashSet<UnityAssetPointer> retained )
	{
		var children = node.Children.Select( child => PruneSceneNode( child, retained ) ).OfType<UnityPrefabNode>().ToArray();
		return children.Length > 0 || node.SpawnPoint || node.Renderers.Length > 0 || node.Lights.Length > 0 ||
			node.Colliders.Length > 0 || node.Particle is not null || node.ReflectionProbe is not null ||
			node.Sprite is not null || node.Line is not null || node.Lod is not null ||
			retained.Contains( node.Source ) ? node with { Children = children } : null;
	}
}

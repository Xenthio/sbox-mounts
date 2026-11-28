namespace UnityMount;

static class RiskOfRain2SceneMarkers
{
	enum Marker { None, SpawnPoint, DisableOnStart, Surface, SceneInfo, ForcedCamera, LookAt, CameraRail, Sound }

	public static UnitySceneMarkers Read( BundleIndex bundles, UnityAssetFile file, IReadOnlyDictionary<uint, string> soundEvents )
	{
		var spawnPoints = new HashSet<UnityAssetPointer>();
		var inactiveAtStart = new HashSet<UnityAssetPointer>();
		var surfaces = new Dictionary<UnityAssetPointer, UnitySurfaceDefinition>();
		var definitions = new Dictionary<UnityAssetPointer, UnitySurfaceDefinition?>();
		var scripts = new Dictionary<int, Marker>();
		var resources = new Dictionary<string, UnityResourceDefinition>( StringComparer.OrdinalIgnoreCase );
		var cameras = new Dictionary<UnityAssetPointer, UnityAuthoredCamera>();
		var lookAt = new Dictionary<UnityAssetPointer, UnityAssetPointer>();
		var rails = new Dictionary<UnityAssetPointer, UnityCameraRail>();
		var sounds = new Dictionary<UnityAssetPointer, List<UnityWwiseCues.Trigger>>();
		foreach ( var asset in file.Data.Objects )
		{
			if ( asset.ClassId != SerializedFile.MonoBehaviourClassId || asset.Tree is not { } tree ) continue;
			var known = scripts.TryGetValue( asset.TypeIndex, out var marker );
			if ( known && marker == Marker.None ) continue;

			using var reader = new UnityReader( file.ReadObject( asset, 4096 ), leaveOpen: false );
			if ( !known )
				scripts.Add( asset.TypeIndex, marker = ReadMarker( bundles, file, reader, tree ) );
			if ( marker == Marker.None ) continue;
			if ( marker == Marker.SceneInfo )
			{
				RegisterGraph( reader, tree, "groundNodesAsset", "navigation/ground.ror2nav" );
				RegisterGraph( reader, tree, "airNodesAsset", "navigation/air.ror2nav" );
				continue;
			}
			if ( marker != Marker.Surface && !UnitySceneObjects.ReadComponentEnabled( reader, tree ) ) continue;

			reader.Position = 0;
			tree.SeekField( reader, "m_GameObject" );
			if ( !bundles.TryResolve( file, UnityReference.Read( reader ), UnitySceneObjects.GameObjectClassId, out var ownerFile, out var owner ) )
				continue;
			var target = new UnityAssetPointer( ownerFile, owner );
			switch ( marker )
			{
				case Marker.SpawnPoint:
					spawnPoints.Add( target );
					break;
				case Marker.DisableOnStart:
					inactiveAtStart.Add( target );
					break;
				case Marker.ForcedCamera:
					if ( ReadCamera( bundles, file, asset, reader, tree ) is { } camera ) cameras.TryAdd( target, camera );
					break;
				case Marker.CameraRail:
					if ( RiskOfRain2CameraRail.Read( bundles, file, reader, tree ) is { } rail ) rails.TryAdd( target, rail );
					break;
				case Marker.Sound:
					if ( UnityWwiseCues.ReadActivation( bundles, file, asset, reader ) is { } sound &&
						soundEvents.ContainsKey( sound.EventId ) )
					{
						if ( !sounds.TryGetValue( target, out var entries ) ) sounds.Add( target, entries = new() );
						entries.Add( sound );
					}
					break;
				case Marker.LookAt:
					reader.Position = 0;
					tree.SeekField( reader, "axis" );
					if ( reader.ReadInt32() != 4 ) break;
					reader.Position = 0;
					tree.SeekField( reader, "target" );
					if ( bundles.TryResolve( file, UnityReference.Read( reader ), UnitySceneObjects.TransformClassId,
						out var lookAtFile, out var lookAtTransform ) )
						lookAt.TryAdd( target, new( lookAtFile, lookAtTransform ) );
					break;
				case Marker.Surface:
					reader.Position = 0;
					tree.SeekField( reader, "surfaceDef" );
					if ( bundles.TryResolve( file, UnityReference.Read( reader ), SerializedFile.MonoBehaviourClassId, out var definitionFile, out var definition ) )
					{
						var key = new UnityAssetPointer( definitionFile, definition );
						if ( !definitions.TryGetValue( key, out var surface ) )
							definitions.Add( key, surface = RiskOfRain2Surfaces.Read( bundles, key ) );
						if ( surface is { } value ) surfaces.TryAdd( target, value );
					}
					break;
			}
		}
		foreach ( var owner in cameras.Keys.ToArray() )
		{
			var camera = cameras[owner];
			cameras[owner] = camera with { LookAt = lookAt.TryGetValue( owner, out var target ) ? target : null, Rail = rails.GetValueOrDefault( owner ) };
		}
		return new( spawnPoints, inactiveAtStart, surfaces, resources, cameras, sounds, soundEvents );

		void RegisterGraph( UnityReader reader, UnityTypeTree tree, string field, string path )
		{
			reader.Position = 0;
			tree.SeekField( reader, field );
			if ( bundles.TryResolve( file, UnityReference.Read( reader ), SerializedFile.MonoBehaviourClassId, out var owner, out var graph ) )
				resources.TryAdd( path, new( ResourceType.GameResource, () => new RiskOfRain2NodeGraphLoader( owner, graph ) ) );
		}
	}

	static UnityAuthoredCamera ReadCamera( BundleIndex bundles, UnityAssetFile file, SerializedFile.Object asset,
		UnityReader reader, UnityTypeTree tree )
	{
		var fov = ReadFloat( "fovOverride" );
		var entry = ReadFloat( "entryLerpDuration" );
		var exit = ReadFloat( "exitLerpDuration" );
		if ( !float.IsFinite( fov ) || fov >= 180 || !float.IsFinite( entry ) || !float.IsFinite( exit ) || entry < 0 || exit < 0 ) return null;
		reader.Position = 0;
		tree.SeekField( reader, "nextCamera" );
		UnityAssetPointer? next = bundles.TryResolve( file, UnityReference.Read( reader ), SerializedFile.MonoBehaviourClassId,
			out var nextFile, out var nextAsset ) ? new( nextFile, nextAsset ) : null;
		return new( new( file, asset ), fov, entry, exit, next, null );

		float ReadFloat( string name )
		{
			reader.Position = 0;
			tree.SeekField( reader, name );
			return reader.ReadSingle();
		}
	}

	static Marker ReadMarker( BundleIndex bundles, UnityAssetFile file, UnityReader reader, UnityTypeTree tree )
	{
		if ( !tree.HasField( "m_Script" ) || !tree.HasField( "m_Enabled" ) || !tree.HasField( "m_GameObject" ) )
			return Marker.None;
		tree.SeekField( reader, "m_Script" );
		if ( !bundles.TryResolve( file, UnityReference.Read( reader ), SerializedFile.MonoScriptClassId, out var owner, out var script ) ||
			script.Tree is null ) return Marker.None;

		using var metadata = new UnityReader( owner.ReadObject( script, 4096 ), leaveOpen: false );
		var name = ReadString( "m_ClassName" );
		if ( name == "AkEvent" && ReadString( "m_AssemblyName" ) == "AK.Wwise.Unity.MonoBehaviour.dll" &&
			tree.HasField( "triggerList" ) && tree.HasField( "data" ) ) return Marker.Sound;
		if ( name is not ("SpawnPoint" or "DisableOnStart" or "SurfaceDefProvider" or "SceneInfo" or "ForcedCamera" or "LookAtTransform" or "PositionAlongBasicBezierSpline") ||
			ReadString( "m_AssemblyName" ) != "RoR2.dll" )
			return Marker.None;
		return (name, ReadString( "m_Namespace" )) switch
		{
			("SpawnPoint", "RoR2") => Marker.SpawnPoint,
			("DisableOnStart", "") => Marker.DisableOnStart,
			("SurfaceDefProvider", "RoR2") when tree.HasField( "surfaceDef" ) => Marker.Surface,
			("SceneInfo", "RoR2") when tree.HasField( "groundNodesAsset" ) && tree.HasField( "airNodesAsset" ) => Marker.SceneInfo,
			("ForcedCamera", "RoR2") when tree.HasField( "fovOverride" ) && tree.HasField( "nextCamera" ) => Marker.ForcedCamera,
			("LookAtTransform", "RoR2") when tree.HasField( "axis" ) && tree.HasField( "target" ) => Marker.LookAt,
			("PositionAlongBasicBezierSpline", "RoR2") when tree.HasField( "curve" ) && tree.HasField( "normalizedPositionAlongCurve" ) => Marker.CameraRail,
			_ => Marker.None
		};

		string ReadString( string field )
		{
			metadata.Position = 0;
			script.Tree.SeekField( metadata, field );
			return metadata.ReadAlignedString();
		}
	}
}

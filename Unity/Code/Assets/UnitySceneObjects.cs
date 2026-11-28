namespace UnityMount;

static class UnitySceneObjects
{
	public const int AssetBundleClassId = 142;
	public const int GameObjectClassId = 1;
	public const int TransformClassId = 4;
	public const int LodGroupClassId = 205;
	public const int RectTransformClassId = 224;

	public static string LayerTag( int layer ) => $"unity-layer-{layer}";

	internal readonly record struct Asset( string Path, UnityReference Reference );
	internal readonly record struct SceneArchive( string Path, string Archive );

	public static SceneArchive[] ReadSceneArchives( UnityReader reader, UnityTypeTree tree )
	{
		var paths = ReadAssets( reader, tree )
			.Where( asset => Path.GetExtension( asset.Path ).Equals( ".unity", StringComparison.OrdinalIgnoreCase ) )
			.Select( asset => asset.Path ).ToArray();
		if ( paths.Length == 0 ) return [];

		var archives = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );
		if ( tree.HasField( "m_SceneHashes" ) )
		{
			reader.Position = 0;
			tree.SeekField( reader, "m_SceneHashes" ).VisitArray( reader, (_, _) =>
				archives.TryAdd( reader.ReadAlignedString(), reader.ReadAlignedString() ) );
		}
		if ( paths.Length > 1 && paths.Any( path => !archives.ContainsKey( path ) ) )
			throw new InvalidDataException( "Unity scene manifest does not identify every scene archive." );
		return paths.Select( path => new SceneArchive( path, archives.GetValueOrDefault( path ) ) ).ToArray();
	}
	internal readonly record struct GameObjectData( string Name, bool Active, uint Layer, UnityReference[] Components );
	internal readonly record struct TransformData( UnityReference Owner, UnityReference Parent, Transform Local, UnityReference[] Children );

	public static Asset[] ReadAssets( UnityReader reader, UnityTypeTree tree )
	{
		tree.SeekField( reader, "m_Container" );
		var count = reader.ReadInt32();
		if ( count < 0 || count > reader.Remaining / 24 )
			throw new InvalidDataException( "Invalid Unity asset container count." );

		var assets = new Asset[count];
		for ( var i = 0; i < assets.Length; i++ )
		{
			var path = reader.ReadAlignedString();
			reader.Skip( 8 ); // Preload index and size.
			assets[i] = new( path, UnityReference.Read( reader ) );
		}
		return assets;
	}

	public static GameObjectData ReadGameObject( UnityReader reader, UnityTypeTree tree )
	{
		tree.SeekField( reader, "m_Component" );
		var components = UnityReference.ReadArray( reader );
		reader.Position = 0;
		tree.SeekField( reader, "m_Layer" );
		var layer = reader.ReadUInt32();
		reader.Position = 0;
		tree.SeekField( reader, "m_Name" );
		var name = reader.ReadAlignedString();
		reader.Position = 0;
		tree.SeekField( reader, "m_IsActive" );
		return new( name, reader.ReadBoolean(), layer, components );
	}

	public static TransformData ReadTransform( UnityReader reader, UnityTypeTree tree )
	{
		tree.SeekField( reader, "m_GameObject" );
		var owner = UnityReference.Read( reader );
		reader.Position = 0;
		tree.SeekField( reader, "m_LocalRotation" );
		var rotation = new System.Numerics.Quaternion( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
		var position = new System.Numerics.Vector3( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
		var scale = new System.Numerics.Vector3( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
		if ( !float.IsFinite( rotation.LengthSquared() ) || rotation.LengthSquared() == 0 ||
			!float.IsFinite( position.LengthSquared() ) || !float.IsFinite( scale.LengthSquared() ) )
			throw new InvalidDataException( "Invalid Unity local transform." );

		reader.Position = 0;
		tree.SeekField( reader, "m_Children" );
		var children = UnityReference.ReadArray( reader );
		reader.Position = 0;
		tree.SeekField( reader, "m_Father" );
		return new( owner, UnityReference.Read( reader ),
			new( UnityCoordinates.Position( position ), UnityCoordinates.RotationFor( rotation ), UnityCoordinates.Scale( scale ) ), children );
	}

	public static bool ReadComponentEnabled( UnityReader reader, UnityTypeTree tree )
	{
		reader.Position = 0;
		tree.SeekField( reader, "m_Enabled" );
		return reader.ReadBoolean();
	}

	public readonly record struct LodLevel( float ScreenHeight, UnityReference[] Renderers );
	public sealed record LodGroup( Vector3 Center, float Size, LodLevel[] Levels );

	public static LodGroup ReadLodGroup( UnityReader reader, UnityTypeTree tree )
	{
		if ( !ReadComponentEnabled( reader, tree ) ) return null;
		reader.Position = 0;
		tree.SeekField( reader, "m_LocalReferencePoint" );
		var center = UnityCoordinates.Position( new( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() ) );
		reader.Position = 0;
		tree.SeekField( reader, "m_Size" );
		var size = UnityCoordinates.ToInches( reader.ReadSingle() );
		if ( !float.IsFinite( center.LengthSquared ) || !float.IsFinite( size ) || size <= 0 ) return null;
		var levels = ReadLodLevels( reader, tree );
		return levels.Length > 0 && levels.All( level => float.IsFinite( level.ScreenHeight ) && level.ScreenHeight is >= 0 and <= 1 )
			? new( center, size, levels ) : null;
	}

	static LodLevel[] ReadLodLevels( UnityReader reader, UnityTypeTree tree )
	{
		reader.Position = 0;
		var levels = new List<LodLevel>();
		tree.SeekField( reader, "m_LODs" ).VisitArray( reader, (level, start) =>
		{
			level.SeekField( reader, "screenRelativeHeight" );
			var height = reader.ReadSingle();
			reader.Position = start;
			level.SeekField( reader, "renderers" );
			var renderers = UnityReference.ReadArray( reader );
			levels.Add( new( height, renderers ) );
		} );
		return levels.ToArray();
	}
}

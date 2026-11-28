namespace UnityMount;

readonly record struct UnityAssetPointer( UnityAssetFile File, SerializedFile.Object Asset );

sealed class UnityAddressedAssets( BundleIndex bundles, UnityAddressables catalog )
{
	readonly Dictionary<string, List<(UnityAssetFile File, UnityReference Reference)>> _assets = new( StringComparer.OrdinalIgnoreCase );

	public void Index( UnityAssetFile file )
	{
		foreach ( var asset in file.Data.Objects )
		{
			if ( asset.ClassId != UnitySceneObjects.AssetBundleClassId || asset.Tree is null ) continue;
			const int maxSize = 4 * 1024 * 1024;
			if ( asset.Size > maxSize ) throw new InvalidDataException( "Unity asset container is too large." );
			using var reader = new UnityReader( file.ReadObject( asset, maxSize ), leaveOpen: false );
			foreach ( var entry in UnitySceneObjects.ReadAssets( reader, asset.Tree ) )
			{
				if ( !_assets.TryGetValue( entry.Path, out var candidates ) )
					_assets.Add( entry.Path, candidates = new() );
				candidates.Add( (file, entry.Reference) );
			}
		}
	}

	public UnityAssetPointer? Resolve( string guid, string name, int classId )
	{
		if ( string.IsNullOrEmpty( guid ) ) return null;

		foreach ( var path in catalog.GetPaths( guid ) )
		{
			if ( !_assets.TryGetValue( path, out var candidates ) ) continue;
			foreach ( var (owner, reference) in candidates )
			{
				if ( !bundles.TryResolve( owner, reference, classId, out var file, out var asset ) || asset.Tree is null ) continue;
				if ( !string.IsNullOrEmpty( name ) )
				{
					using var reader = new UnityReader( file.ReadObject( asset, 4104 ), leaveOpen: false );
					asset.Tree.SeekField( reader, "m_Name" );
					if ( reader.ReadAlignedString() != name ) continue;
				}
				return new( file, asset );
			}
		}
		return null;
	}
}

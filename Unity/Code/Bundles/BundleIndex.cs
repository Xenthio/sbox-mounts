namespace UnityMount;

/// <summary>Indexes serialized archive names without unpacking asset data in dependency bundles.</summary>
sealed class BundleIndex( BaseFileSystem fileSystem, string directory, string builtinResourceDirectory )
{
	const string ArchivePrefix = "archive:/";
	const string SharedAssetsSuffix = ".sharedAssets";

	readonly object _indexLock = new();
	readonly UnityTypeSchemas _schemas = new();
	readonly UnityBundleBlockCache _blockCache = new();
	readonly Dictionary<string, UnityAssetFile> _standalone = new( StringComparer.OrdinalIgnoreCase );
	UnityAssetFile[] _schemaFiles = [];

	readonly Dictionary<string, UnityAssetFile> _byArchive = new( StringComparer.OrdinalIgnoreCase );
	readonly Dictionary<string, UnityAssetFile[]> _byBundle = new( StringComparer.OrdinalIgnoreCase );
	bool _indexedDependencies;

	public void UseSchemasFrom( UnityAssetFile[] files ) => _schemaFiles = files;

	public IEnumerable<UnityAssetFile> FindGroup( string group ) => FindGroup( group, "assets" );

	public IEnumerable<UnityAssetFile> FindSceneManifests( string group )
	{
		foreach ( var file in fileSystem.FindFile( directory, $"{group}_scenes_all_*.bundle", false ).Order( StringComparer.Ordinal ) )
		{
			var entries = IndexBundle( file, group );
			var shared = entries.Where( entry => entry.Entry.Name.EndsWith( SharedAssetsSuffix, StringComparison.OrdinalIgnoreCase ) ).ToArray();
			foreach ( var manifest in shared.Length > 0 ? shared : entries )
				yield return manifest;
		}
	}

	public UnityAssetFile FindSceneObjects( UnityAssetFile manifest, string archive )
	{
		if ( string.IsNullOrEmpty( archive ) )
		{
			if ( !manifest.Entry.Name.EndsWith( SharedAssetsSuffix, StringComparison.OrdinalIgnoreCase ) ) return manifest;
			archive = manifest.Entry.Name[..^SharedAssetsSuffix.Length];
		}
		lock ( _indexLock )
			return _byArchive.TryGetValue( archive, out var scene ) && scene.Bundle == manifest.Bundle ? scene : null;
	}

	IEnumerable<UnityAssetFile> FindGroup( string group, string kind )
	{
		foreach ( var file in fileSystem.FindFile( directory, $"{group}_{kind}_all_*.bundle", false ).Order( StringComparer.Ordinal ) )
		{
			foreach ( var assetFile in IndexBundle( file, group ) )
				yield return assetFile;
		}
	}

	public bool TryResolve( UnityAssetFile owner, UnityReference reference, int classId, out UnityAssetFile file, out SerializedFile.Object asset )
	{
		file = owner;
		asset = default;
		if ( reference.Id == 0 || reference.FileId < 0 ) return false;
		if ( reference.FileId != 0 )
		{
			var externals = owner.Data.Externals;
			if ( reference.FileId > externals.Length ) return false;

			file = ResolveExternal( externals[reference.FileId - 1] );
		}

		return file is not null && file.Data.TryGetObject( reference.Id, out asset ) && asset.ClassId == classId;
	}

	UnityAssetFile ResolveExternal( string path )
	{
		lock ( _indexLock )
			return ResolveExternalLocked( path );
	}

	UnityAssetFile ResolveExternalLocked( string path )
	{
		if ( path.StartsWith( ArchivePrefix, StringComparison.OrdinalIgnoreCase ) )
		{
			var archive = path[(path.LastIndexOf( '/' ) + 1)..];
			if ( !_byArchive.TryGetValue( archive, out var file ) && !_indexedDependencies )
			{
				IndexDependencies();
				_byArchive.TryGetValue( archive, out file );
			}
			return file;
		}

		if ( builtinResourceDirectory is null ) return null;
		var name = path.ToLowerInvariant() switch
		{
			"library/unity default resources" => "unity default resources",
			"resources/unity_builtin_extra" => "unity_builtin_extra",
			_ => null
		};
		return name is null ? null : IndexSerializedFileLocked( $"{builtinResourceDirectory}/{name}", name.Replace( ' ', '-' ) );
	}

	public UnityAssetFile FindSerializedFile( string path, string group )
	{
		lock ( _indexLock )
			return IndexSerializedFileLocked( path, group );
	}

	UnityAssetFile IndexSerializedFileLocked( string path, string group )
	{
		if ( _standalone.TryGetValue( path, out var cached ) ) return cached;

		try
		{
			if ( !fileSystem.FileExists( path ) ) return _standalone[path] = null;

			// The player's resource files omit their layouts. Only identical type hashes can share a schema.
			foreach ( var schemaFile in _schemaFiles )
			{
				try { _ = schemaFile.Data; }
				catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
				{
					Log.Warning( $"Could not read Unity schemas from '{schemaFile.Entry.Name}': {e.Message}" );
				}
			}
			_schemaFiles = [];

			UnityBundle.Entry? sidecar = null;
			var sidecarPath = path + ".resS";
			if ( fileSystem.FileExists( sidecarPath ) )
			{
				using var payload = fileSystem.OpenRead( sidecarPath );
				sidecar = new( Path.GetFileName( sidecarPath ), 0, payload.Length, false, sidecarPath );
			}

			using var stream = fileSystem.OpenRead( path );
			var bundle = UnityBundle.OpenSerializedFile( path, stream.Length, sidecar );
			var assetFile = new UnityAssetFile( fileSystem, bundle, bundle.Entries[0], group, _schemas );
			_ = assetFile.Data;
			return _standalone[path] = assetFile;
		}
		catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
		{
			Log.Warning( $"Could not index standalone Unity resources '{path}': {e.Message}" );
			return _standalone[path] = null;
		}
	}

	void IndexDependencies()
	{
		_indexedDependencies = true;
		foreach ( var file in fileSystem.FindFile( directory, "*.bundle", false ).Order( StringComparer.Ordinal ) )
		{
			var group = Path.GetFileNameWithoutExtension( file );
			var suffix = Math.Max( group.LastIndexOf( "_assets_all_", StringComparison.Ordinal ),
				group.LastIndexOf( "_scenes_all_", StringComparison.Ordinal ) );
			if ( suffix > 0 ) group = group[..suffix];
			IndexBundleLocked( file, group );
		}
	}

	UnityAssetFile[] IndexBundle( string file, string group )
	{
		lock ( _indexLock )
			return IndexBundleLocked( file, group );
	}

	UnityAssetFile[] IndexBundleLocked( string file, string group )
	{
		if ( _byBundle.TryGetValue( file, out var cached ) ) return cached;

		var path = $"{directory}/{file}";
		UnityAssetFile[] result = [];
		try
		{
			using var stream = fileSystem.OpenRead( path );
			var bundle = UnityBundle.Read( stream, path, _blockCache );
			result = bundle.Entries.Where( entry => entry.Serialized )
				.Select( entry => new UnityAssetFile( fileSystem, bundle, entry, group, _schemas ) ).ToArray();
			foreach ( var assetFile in result )
				_byArchive.TryAdd( assetFile.Entry.Name, assetFile );
		}
		catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
		{
			Log.Warning( $"Could not index Unity bundle '{file}': {e.Message}" );
		}

		_byBundle.Add( file, result );
		return result;
	}
}

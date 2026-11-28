namespace UnityMount;

sealed class UnityAssetFile( BaseFileSystem fileSystem, UnityBundle bundle, UnityBundle.Entry entry, string group, UnityTypeSchemas schemas )
{
	public UnityBundle Bundle { get; } = bundle;
	public UnityBundle.Entry Entry { get; } = entry;
	public string Group { get; } = group;

	readonly Lazy<SerializedFile> _data = new( () =>
	{
		using var stream = fileSystem.OpenRead( bundle.Path );
		return SerializedFile.Read( bundle, stream, entry, schemas );
	} );
	public SerializedFile Data => _data.Value;

	readonly Dictionary<long, UnityAnimationClip> _animations = new();

	public UnityAnimationClip ReadAnimation( SerializedFile.Object asset )
	{
		lock ( _animations )
		{
			if ( _animations.TryGetValue( asset.Id, out var cached ) ) return cached;
			var clip = ReadAnimationClip( asset );
			_animations.Add( asset.Id, clip );
			return clip;
		}
	}

	public UnityAnimationClip ReadLightAnimation( SerializedFile.Object asset ) => ReadAnimationClip( asset, lightOnly: true );

	UnityAnimationClip ReadAnimationClip( SerializedFile.Object asset, bool lightOnly = false )
	{
		if ( !Data.UnityVersion.StartsWith( "2021.3.", StringComparison.Ordinal ) ||
			asset.ClassId != UnityAnimationClip.ClassId || asset.Tree is null || asset.Size > 4 * 1024 * 1024 ) return null;

		try
		{
			using var reader = new UnityReader( ReadObject( asset, asset.Size ), leaveOpen: false );
			return UnityAnimationClip.Read( reader, asset.Tree, lightOnly );
		}
		catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
		{
			Log.Warning( $"Could not read Unity animation {asset.Id} in '{Entry.Name}': {e.Message}" );
			return null;
		}
	}

	public Stream OpenBundle() => fileSystem.OpenRead( Bundle.Path );

	public MemoryStream ReadObject( SerializedFile.Object asset, int maxSize )
	{
		using var stream = OpenBundle();
		var bytes = Bundle.ReadRange( stream, Entry, asset.Offset, Math.Min( asset.Size, maxSize ) );
		return new( bytes, writable: false );
	}
}

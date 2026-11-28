namespace UnityMount;

sealed partial class UnityResourceRegistry
{
	readonly Dictionary<UnityAssetPointer, (string Path, int Images)> _billboards = new();

	UnityPrefabRenderer? ReadBillboard( UnityAssetFile file, SerializedFile.Object asset )
	{
		if ( asset.Tree is null ) return null;
		try
		{
			using var reader = ReadMetadata( file, asset );
			var enabled = UnitySceneObjects.ReadComponentEnabled( reader, asset.Tree );
			reader.Position = 0;
			asset.Tree.SeekField( reader, "m_CastShadows" );
			var shadows = reader.ReadByte() != 0;
			reader.Position = 0;
			asset.Tree.SeekField( reader, "m_Billboard" );
			if ( !bundles.TryResolve( file, UnityReference.Read( reader ), UnityBillboard.ClassId, out var owner, out var billboard ) )
				return null;
			var (path, images) = RegisterBillboard( new( owner, billboard ) );
			if ( path is null ) return null;
			return new( path, false, enabled, Source: new( file, asset ), BillboardImages: images, BillboardShadows: shadows );
		}
		catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
		{
			Log.Warning( $"Could not read Unity billboard renderer {asset.Id} in '{file.Entry.Name}': {e.Message}" );
			return null;
		}
	}

	(string Path, int Images) RegisterBillboard( UnityAssetPointer pointer )
	{
		if ( _billboards.TryGetValue( pointer, out var cached ) ) return cached;
		_billboards.Add( pointer, default );
		var (file, asset) = pointer;
		if ( asset.Tree is null ) return default;
		using var reader = ReadMetadata( file, asset );
		if ( UnityBillboard.Read( reader, asset.Tree ) is not { } source ||
			!bundles.TryResolve( file, source.Material, UnityMaterial.ClassId, out var materialFile, out var material ) ||
			RegisterMaterial( materialFile, material ).Path is not { } materialPath ) return default;

		var name = SanitizeResourceName( source.Name );
		if ( !IsResourceName( name ) || ReservePath( $"models/{file.Group}/billboards/{name}", ".vmdl", asset.Id ) is not { } path )
			return default;
		Define( ResourceType.Model, path, () => new UnityBillboardModelLoader( source, materialPath ) );
		return _billboards[pointer] = (ResourcePath( path ), source.Images.Length);
	}
}

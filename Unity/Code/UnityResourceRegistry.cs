namespace UnityMount;

sealed record UnityResourceDefinition( ResourceType Type, Func<ResourceLoader> Create );

/// <summary>Registers resources for the selected files and resolves their referenced assets on demand.</summary>
sealed partial class UnityResourceRegistry( string ident, BundleIndex bundles, UnityVisualOverrides overrides, float particleGravity,
	string parentPath = null, UnityResourceRegistry.SharedAssets shared = null )
{
	public sealed record SharedAssets(
		IReadOnlyDictionary<(UnityAssetFile File, long Id, bool PackedNormal), UnityMountedTexture> Textures,
		IReadOnlyDictionary<(UnityAssetFile File, long Id), (string Name, string Path)> Materials,
		IReadOnlyDictionary<UnityAssetPointer, UnityMountedSprite?> Sprites,
		IReadOnlyDictionary<UnityAssetPointer, string> Prefabs );

	readonly HashSet<string> _paths = new( StringComparer.OrdinalIgnoreCase );
	readonly Dictionary<string, UnityResourceDefinition> _resources = new( StringComparer.OrdinalIgnoreCase );
	readonly Dictionary<(UnityAssetFile File, long Id, bool PackedNormal), UnityMountedTexture> _textures = new();
	readonly Dictionary<(UnityAssetFile File, long Id), (string Name, string Path)> _materials = new();
	readonly Dictionary<UnityAssetPointer, UnityMountedSprite?> _sprites = new();
	readonly Dictionary<UnityAssetPointer, UnityShader> _shaders = new();

	public SharedAssets GetSharedAssets() => new( _textures, _materials, _sprites, _prefabPaths );

	public bool Publish( MountContext context )
	{
		foreach ( var (path, definition) in _resources )
			context.Add( definition.Type, path, definition.Create() );
		return _resources.Count > 0;
	}

	void Define( ResourceType type, string path, Func<ResourceLoader> create ) => _resources.Add( path, new( type, create ) );

	string ResourcePath( string path ) => parentPath is null ? $"mount://{ident}/{path}" : $"mount://{ident}/{parentPath}/{path}";

	public void Register( UnityAssetFile file )
	{
		foreach ( var asset in file.Data.Objects )
			if ( asset.ClassId is UnityTexture.ClassId or UnityTexture.CubemapClassId ) RegisterTexture( file, asset );

		foreach ( var asset in file.Data.Objects )
			if ( asset.ClassId == UnityMaterial.ClassId ) RegisterMaterial( file, asset );

		foreach ( var asset in file.Data.Objects )
			if ( asset.ClassId == UnitySprite.ClassId ) RegisterSprite( new( file, asset ) );
		RegisterModels( file );
	}

	UnityMountedSprite? RegisterSprite( UnityAssetPointer pointer )
	{
		if ( _sprites.TryGetValue( pointer, out var cached ) ) return cached;
		if ( shared?.Sprites.TryGetValue( pointer, out var sharedSprite ) == true ) return sharedSprite;
		var (file, asset) = pointer;
		_sprites.Add( pointer, null );
		if ( asset.Tree is null ) return null;
		try
		{
			using var reader = ReadMetadata( file, asset );
			var sprite = UnitySprite.Read( reader, asset.Tree );
			if ( sprite is null ) return null;
			var name = SanitizeResourceName( sprite.Name );
			if ( !IsResourceName( name ) || RegisterTexture( file, sprite.Texture ) is not { } texture )
				return null;

			var path = ReservePath( $"sprites/{file.Group}/{name}", ".sprite", asset.Id );
			if ( path is null ) return null;
			Define( ResourceType.GameResource, path, () => new UnitySpriteLoader( sprite, texture ) );
			var size = UnityCoordinates.ToInches( Math.Max( sprite.Width, sprite.Height ) / sprite.PixelsPerUnit );
			return _sprites[pointer] = new UnityMountedSprite( ResourcePath( path ), size );
		}
		catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
		{
			Log.Warning( $"Could not index Unity sprite {asset.Id} in '{file.Entry.Name}': {e.Message}" );
			return null;
		}
	}

	string RegisterTexture( UnityAssetFile file, UnityReference reference, bool packNormal = false ) =>
		RegisterTextureBinding( file, reference, packNormal ).Path;

	UnityMountedTexture RegisterTextureBinding( UnityAssetFile file, UnityReference reference, bool packNormal = false ) =>
		bundles.TryResolve( file, reference, UnityTexture.ClassId, out var textureFile, out var texture )
			? RegisterTextureBinding( textureFile, texture, packNormal ) : default;

	string RegisterTexture( UnityAssetFile file, SerializedFile.Object asset ) => RegisterTextureBinding( file, asset ).Path;

	UnityMountedTexture RegisterTextureBinding( UnityAssetFile file, SerializedFile.Object asset, bool packNormal = false )
	{
		var key = (file, asset.Id, packNormal);
		if ( _textures.TryGetValue( key, out var cached ) ) return cached;
		if ( shared?.Textures.TryGetValue( key, out var sharedTexture ) == true ) return sharedTexture;
		_textures.Add( key, default );
		if ( !file.Data.UnityVersion.StartsWith( "2021.3.", StringComparison.Ordinal ) ) return default;

		try
		{
			using var stream = file.OpenBundle();
			var texture = UnityTexture.Read( file.Bundle, stream, file.Entry, asset );
			if ( texture is null ) return default;
			var name = SanitizeResourceName( texture.Name );
			if ( !IsResourceName( name ) ) return default;

			if ( packNormal && !texture.SupportsPackedNormal ) return default;

			var directory = texture.Cube ? "textures/cubemaps" : packNormal ? "textures/packed_normals" : "textures";
			var path = ReservePath( $"{directory}/{file.Group}/{name}", ".vtex", asset.Id );
			if ( path is null ) return default;
			Define( ResourceType.Texture, path, () => texture.Cube ? new UnityCubemapLoader( file.Bundle, texture )
				: new UnityTextureLoader( file.Bundle, texture, packNormal ) );
			return _textures[key] = new( ResourcePath( path ), texture.Sampler );
		}
		catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
		{
			Log.Warning( $"Could not index Unity texture {asset.Id} in '{file.Entry.Name}': {e.Message}" );
			return default;
		}
	}

	(string Name, string Path) RegisterMaterial( UnityAssetFile file, SerializedFile.Object asset ) => RegisterParsedMaterial( file, asset, null );

	(string Name, string Path) RegisterParsedMaterial( UnityAssetFile file, SerializedFile.Object asset, UnityMaterial parsed )
	{
		var key = (file, asset.Id);
		if ( _materials.TryGetValue( key, out var cached ) ) return cached;
		if ( shared?.Materials.TryGetValue( key, out var sharedMaterial ) == true ) return sharedMaterial;
		_materials.Add( key, default );
		if ( asset.Tree is null ) return default;

		try
		{
			var source = parsed;
			if ( source is null )
			{
				using var reader = ReadMetadata( file, asset );
				source = UnityMaterial.Read( reader, asset.Tree, reference => ReadShader( file, reference ) );
			}
			var name = SanitizeResourceName( source.Name );
			if ( !IsResourceName( name ) ) return default;
			var path = ReservePath( $"materials/{file.Group}/{name}", ".vmat", asset.Id );
			if ( path is null ) return default;
			if ( source.Distortion is { } distortion )
			{
				var normal = RegisterTextureBinding( file, distortion.Normal.Reference );
				var mask = RegisterTextureBinding( file, distortion.Mask.Reference );
				Define( ResourceType.Material, path, () => new UnityDistortionMaterialLoader( distortion, normal, mask ) );
				return _materials[key] = (name, ResourcePath( path ));
			}
			if ( source.Cloud is { } cloud )
			{
				var main = RegisterTextureBinding( file, cloud.Main.Reference );
				var first = RegisterTextureBinding( file, cloud.First.Reference );
				var second = RegisterTextureBinding( file, cloud.Second.Reference );
				var remap = RegisterTextureBinding( file, cloud.Remap );
				Define( ResourceType.Material, path, () => new UnityCloudMaterialLoader( cloud, main, first, second, remap ) );
				return _materials[key] = (name, ResourcePath( path ));
			}
			if ( source.OpaqueCloud is { } opaque )
			{
				var main = RegisterTextureBinding( file, opaque.Main.Reference );
				var first = RegisterTextureBinding( file, opaque.First.Reference );
				var second = RegisterTextureBinding( file, opaque.Second.Reference );
				var remap = RegisterTextureBinding( file, opaque.Remap );
				var normal = RegisterTextureBinding( file, opaque.Normal, packNormal: true );
				Define( ResourceType.Material, path, () => new UnityOpaqueCloudMaterialLoader( opaque, main, first, second, remap, normal ) );
				return _materials[key] = (name, ResourcePath( path ));
			}

			if ( source.Terrain is { } terrain )
			{
				var textures = terrain.Layers.Select( ( layer, i ) =>
					RegisterTextureBinding( file, layer.Reference, packNormal: i == (int)UnityTerrainMaterial.Map.Normal ) ).ToArray();
				var noise = RegisterSnowMicrofacetNoise( terrain.Microfacet );
				Define( ResourceType.Material, path, () => new UnityTerrainMaterialLoader( terrain, textures, noise ) );
				return _materials[key] = (name, ResourcePath( path ));
			}

			if ( source.Snow is { } snow )
			{
				var textures = snow.Layers.Select( ( layer, i ) => RegisterTextureBinding( file, layer.Reference,
					packNormal: i is (int)UnitySnowMaterial.Map.BaseNormal or (int)UnitySnowMaterial.Map.CoverNormal or (int)UnitySnowMaterial.Map.DirtNormal ) ).ToArray();
				var noise = RegisterSnowMicrofacetNoise( snow.Microfacet );
				Define( ResourceType.Material, path, () => new UnitySnowMaterialLoader( snow, textures, noise ) );
				return _materials[key] = (name, ResourcePath( path ));
			}

			if ( source.Standard is { } standard )
			{
				var color = RegisterTextureBinding( file, source.Diffuse );
				var normal = RegisterTextureBinding( file, source.Normal, packNormal: !standard.Flow && !standard.Splat );
				var emission = RegisterTextureBinding( file, source.Emission );
				var mask = RegisterTextureBinding( file, standard.Mask );
				var ramp = RegisterTextureBinding( file, standard.Ramp );
				var layers = standard.Flow || standard.Splat || standard.Print.HasValue
					? standard.Layers.Select( reference => RegisterTextureBinding( file, reference ) ).ToArray() : [];
				Define( ResourceType.Material, path, () => new UnityStandardMaterialLoader( source, color, normal, emission, mask, ramp, layers ) );
				return _materials[key] = (name, ResourcePath( path ));
			}

			if ( source.OptimizedStandard is { } optimizedStandard )
			{
				var textures = optimizedStandard.Layers.Select( layer => RegisterTextureBinding( file, layer.Reference ) ).ToArray();
				Define( ResourceType.Material, path, () => new UnityOptimizedStandardMaterialLoader( optimizedStandard, textures ) );
				return _materials[key] = (name, ResourcePath( path ));
			}

			if ( source.SolidParallax is { } parallax )
			{
				var layers = parallax.Layers.Select( layer => RegisterTextureBinding( file, layer.Reference ) ).ToArray();
				Define( ResourceType.Material, path, () => new UnitySolidParallaxMaterialLoader( parallax, layers ) );
				return _materials[key] = (name, ResourcePath( path ));
			}

			if ( source.WavyCloth is { } cloth )
			{
				var color = RegisterTextureBinding( file, cloth.ColorMap );
				var normal = RegisterTextureBinding( file, cloth.NormalMap );
				Define( ResourceType.Material, path, () => new UnityWavyClothMaterialLoader( cloth, color, normal ) );
				return _materials[key] = (name, ResourcePath( path ));
			}

			var colorTexture = RegisterTexture( file, source.Diffuse );
			var normalTexture = RegisterTexture( file, source.Normal, packNormal: true );
			var emissionTexture = RegisterTexture( file, source.Emission );
			Define( ResourceType.Material, path, () => new UnityMaterialLoader( source, colorTexture, normalTexture, emissionTexture ) );
			return _materials[key] = (name, ResourcePath( path ));
		}
		catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
		{
			Log.Warning( $"Could not index Unity material {asset.Id} in '{file.Entry.Name}': {e.Message}" );
			return default;
		}
	}

	UnityMountedTexture RegisterSnowMicrofacetNoise( bool enabled ) =>
		enabled && overrides.SnowMicrofacetNoise is { } noise ? RegisterTextureBinding( noise.File, noise.Asset ) : default;

	UnityShader ReadShader( UnityAssetFile owner, UnityReference reference )
	{
		try
		{
			if ( !bundles.TryResolve( owner, reference, UnityShader.ClassId, out var file, out var asset ) ||
				asset.Tree is null ) return null;
			var key = new UnityAssetPointer( file, asset );
			if ( _shaders.TryGetValue( key, out var cached ) ) return cached;
			_shaders.Add( key, null );
			using var reader = ReadMetadata( file, asset );
			return _shaders[key] = UnityShader.Read( reader, asset.Tree );
		}
		catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
		{
			Log.Warning( $"Could not read Unity shader {reference.Id} referenced by '{owner.Entry.Name}': {e.Message}" );
			return null;
		}
	}

	string ReservePath( string stem, string extension, long id )
	{
		var path = stem + extension;
		if ( _paths.Add( path ) ) return path;
		path = $"{stem}_{id:x}{extension}";
		return _paths.Add( path ) ? path : null;
	}

	static UnityReader ReadMetadata( UnityAssetFile file, SerializedFile.Object asset )
	{
		const int maxSize = 4 * 1024 * 1024;
		if ( asset.Size > maxSize ) throw new InvalidDataException( "Unity object metadata is too large." );
		return new( file.ReadObject( asset, maxSize ), leaveOpen: false );
	}

	static string SanitizeResourceName( string name ) => string.Concat( name.Select( character => IsResourceNameCharacter( character ) ? character : '_' ) );

	static bool IsResourceName( string name ) => !string.IsNullOrWhiteSpace( name ) && name is not ("." or "..") &&
		name.All( IsResourceNameCharacter );

	static bool IsResourceNameCharacter( char character ) => char.IsAsciiLetterOrDigit( character ) || character is '_' or '-' or '.' or ' ';
}

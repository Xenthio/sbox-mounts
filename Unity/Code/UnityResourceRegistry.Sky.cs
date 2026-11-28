namespace UnityMount;

sealed partial class UnityResourceRegistry
{
	readonly Dictionary<UnityAssetPointer, UnitySceneSky?> _skies = new();

	UnitySceneSky? RegisterSceneSky( UnityAssetFile file, UnityReference skybox )
	{
		try
		{
			if ( !bundles.TryResolve( file, skybox, UnityMaterial.ClassId, out var materialFile, out var material ) ||
				material.Tree is null ) return null;
			var key = new UnityAssetPointer( materialFile, material );
			if ( _skies.TryGetValue( key, out var cached ) ) return cached;
			return _skies[key] = RegisterSkyMaterial( key );
		}
		catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
		{
			Log.Warning( $"Could not index Unity sky in '{file.Entry.Name}': {e.Message}" );
			return null;
		}
	}

	UnitySceneSky? RegisterSkyMaterial( UnityAssetPointer pointer )
	{
		var (file, asset) = pointer;
		using var reader = ReadMetadata( file, asset );
		asset.Tree.SeekField( reader, "m_Shader" );
		if ( !bundles.TryResolve( file, UnityReference.Read( reader ), UnityShader.ClassId, out var shaderFile, out var shader ) ||
			shader.Tree is null ) return null;
		using ( var shaderReader = ReadMetadata( shaderFile, shader ) )
		{
			var parsed = shader.Tree.SeekField( shaderReader, "m_ParsedForm" );
			parsed.SeekField( shaderReader, "m_Name" );
			if ( shaderReader.ReadAlignedString() != "Skybox/Cubemap" ) return null;
		}

		reader.Position = 0;
		var sky = UnitySky.Read( reader, asset.Tree );
		if ( sky is null || !IsResourceName( sky.Name ) ||
			!bundles.TryResolve( file, sky.Cubemap, UnityTexture.CubemapClassId, out var cubeFile, out var cube ) ||
			RegisterTexture( cubeFile, cube ) is not { } texture ) return null;
		var path = ReservePath( $"materials/sky/{file.Group}/{sky.Name}", ".vmat", asset.Id );
		if ( path is null ) return null;
		Define( ResourceType.Material, path, () => new UnitySkyMaterialLoader( texture ) );
		return new( ResourcePath( path ), sky );
	}
}

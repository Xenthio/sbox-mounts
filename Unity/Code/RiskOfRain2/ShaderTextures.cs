namespace UnityMount;

static class RiskOfRain2ShaderTextures
{
	public static UnityAssetPointer? FindSnowNoise( BundleIndex bundles )
	{
		// The player's GlobalShaderTextures component binds this texture to _SnowMicrofacetNoise.
		var file = bundles.FindSerializedFile( "Risk of Rain 2_Data/sharedassets0.assets", "ror2-player" );
		if ( file is null ) return null;
		using var stream = file.OpenBundle();
		foreach ( var asset in file.Data.Objects )
		{
			if ( asset.ClassId != UnityTexture.ClassId ) continue;
			var bytes = file.Bundle.ReadRange( stream, file.Entry, asset.Offset, Math.Min( asset.Size, 4096 ) );
			using var reader = new UnityReader( new MemoryStream( bytes, writable: false ), leaveOpen: false );
			if ( reader.ReadAlignedString() == "texCloudWhitenoisePointFiltered" )
				return new( file, asset );
		}
		return null;
	}
}

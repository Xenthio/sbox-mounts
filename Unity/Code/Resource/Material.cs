namespace UnityMount;

sealed class UnityMaterialLoader( UnityMaterial source, string colorTexture, string normalTexture, string emissionTexture ) : ResourceLoader<GameMount>
{
	protected override object Load()
	{
		var material = Material.Create( Path, "shaders/complex.shader" );
		if ( colorTexture is not null ) material.Set( "g_tColor", Texture.Load( colorTexture ) );
		else if ( source.SimpleOpaque ) material.Set( "g_tColor", Texture.White );
		if ( normalTexture is not null ) material.Set( "g_tNormal", Texture.Load( normalTexture ) );
		if ( source.DoubleSided ) material.SetFeature( "F_RENDER_BACKFACES", 1 );
		if ( source.AlphaCutoff is { } cutoff )
		{
			material.SetFeature( "F_ALPHA_TEST", 1 );
			material.Set( "g_flAlphaTestReference", cutoff );
		}
		if ( emissionTexture is not null && source.EmissionPower > 0 &&
			MathF.Max( source.EmissionTint.r, MathF.Max( source.EmissionTint.g, source.EmissionTint.b ) ) > 0 )
		{
			material.SetFeature( "F_SELF_ILLUM", 1 );
			material.Set( "g_tSelfIllumMask", Texture.Load( emissionTexture ) );
			material.Set( "g_vSelfIllumTint", new Vector3( source.EmissionTint.r, source.EmissionTint.g, source.EmissionTint.b ) );
			material.Set( "g_flSelfIllumAlbedoFactor", 0f );
			material.Set( "g_flSelfIllumScale", 1f );
			material.Set( "g_flSelfIllumBrightness", MathF.Log2( source.EmissionPower ) );
		}
		material.Set( "g_vColorTint", new Vector4( source.Tint.r, source.Tint.g, source.Tint.b, source.Tint.a ) );
		material.Set( "g_vTexCoordScale", source.Scale );
		// Texture rows and mesh UVs are already flipped for s&box.
		material.Set( "g_vTexCoordOffset", new Vector2( source.Offset.x, 1 - source.Scale.y - source.Offset.y ) );
		return material;
	}
}

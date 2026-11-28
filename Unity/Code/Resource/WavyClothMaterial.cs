namespace UnityMount;

sealed class UnityWavyClothMaterialLoader( UnityWavyClothMaterial source, UnityMountedTexture color, UnityMountedTexture normal ) : ResourceLoader<GameMount>
{
	protected override object Load()
	{
		var material = Material.Create( Path, "shaders/unity/wavy_cloth.shader" );
		material.Set( "g_tColor", color.Path is null ? Texture.White : Texture.Load( color.Path ) );
		material.Set( "g_vColorSampler", new Vector2( color.SamplerIndex, color.MirrorV ) );
		if ( normal.Path is not null ) material.Set( "g_tNormal", Texture.Load( normal.Path ) );
		material.Set( "g_vNormalSampler", new Vector2( normal.SamplerIndex, normal.MirrorV ) );
		material.Set( "g_vColorST", source.ColorTransform );
		material.Set( "g_vNormalST", source.NormalTransform );
		material.Set( "g_vTint", source.Tint );
		material.Set( "g_vScroll", source.Scroll );
		material.Set( "g_vWind", source.Wind );
		material.Set( "g_flCutoff", source.Cutoff );
		material.Set( "g_flNormalStrength", normal.Path is null ? 0f : source.NormalStrength );
		material.Set( "g_flVertexOffset", source.VertexOffsetStrength );
		material.Set( "g_flSmoothness", source.Smoothness );
		material.Set( "g_bVertexRed", source.VertexRed ? 1f : 0f );
		return material;
	}
}

namespace UnityMount;

sealed class UnityTerrainMaterialLoader( UnityTerrainMaterial source, UnityMountedTexture[] textures, UnityMountedTexture noise ) : ResourceLoader<GameMount>
{
	static readonly string[] MapNames = [ "RedTop", "RedSide", "Green", "Blue", "Splat", "Normal" ];

	protected override object Load()
	{
		var material = Material.Create( Path, "shaders/unity/triplanar_terrain.shader" );
		for ( var i = 0; i < MapNames.Length; i++ )
		{
			var binding = textures[i];
			var name = MapNames[i];
			if ( binding.Path is not null ) material.Set( $"g_t{name}", Texture.Load( binding.Path ) );
			else if ( i != (int)UnityTerrainMaterial.Map.Normal ) material.Set( $"g_t{name}", source.Optimized.HasValue && i == (int)UnityTerrainMaterial.Map.Splat ? Texture.Black : Texture.White );
			material.Set( $"g_v{name}ST", source.Layers[i].Transform );
			material.Set( $"g_v{name}Sampler", new Vector2(
				binding.SamplerIndex, binding.MirrorV ) );
		}
		material.SetFeature( "F_CULL_MODE", source.Cull );
		material.SetFeature( "F_DOUBLE_SAMPLE", source.DoubleSample ? 1 : 0 );
		material.Set( "g_bHasNormal", textures[(int)UnityTerrainMaterial.Map.Normal].Path is not null ? 1f : 0f );
		material.Set( "g_bVertexColors", source.VertexColors ? 1f : 0f );
		material.Set( "g_bMixVertexColors", source.MixVertexColors ? 1f : 0f );
		material.Set( "g_bAlphaMask", source.AlphaMask ? 1f : 0f );
		material.Set( "g_bVerticalBias", source.VerticalBias ? 1f : 0f );
		material.Set( "g_vTint", new Vector3( source.Tint.r, source.Tint.g, source.Tint.b ) );
		material.Set( "g_flTintAlpha", source.Tint.a );
		material.Set( "g_vBias", source.Bias );
		material.Set( "g_vSmoothness", source.Smoothness );
		material.Set( "g_flFrequency", source.Frequency );
		material.Set( "g_flBlendDepth", source.BlendDepth );
		material.Set( "g_flNormalStrength", source.NormalStrength );
		if ( source.Optimized is { } optimized )
		{
			material.SetFeature( "F_OPTIMIZED", 1 );
			material.Set( "g_vOptimizedTiling", optimized.Tiling );
			material.Set( "g_vOptimizedAlbedo", optimized.Albedo );
			material.Set( "g_vVertexPower", optimized.VertexPower );
			material.Set( "g_vOptimizedUvST", optimized.UvTransform );
		}
		UnitySnowMaterialLoader.BindMicrofacets( material, noise );
		return material;
	}
}

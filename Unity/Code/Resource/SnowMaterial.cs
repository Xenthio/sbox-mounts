namespace UnityMount;

sealed class UnitySnowMaterialLoader( UnitySnowMaterial source, UnityMountedTexture[] textures, UnityMountedTexture noise ) : ResourceLoader<GameMount>
{
	static readonly string[] MapNames = [ "Base", "BaseNormal", "Cover", "CoverNormal", "Dirt", "DirtNormal" ];

	protected override object Load()
	{
		var material = Material.Create( Path, "shaders/unity/snow_topped.shader" );
		var boundNormals = Vector2.Zero;
		for ( var i = 0; i < MapNames.Length; i++ )
		{
			var binding = textures[i];
			var name = MapNames[i];
			if ( binding.Path is not null ) material.Set( $"g_t{name}", Texture.Load( binding.Path ) );
			else if ( i is (int)UnitySnowMaterial.Map.Base or (int)UnitySnowMaterial.Map.Cover or (int)UnitySnowMaterial.Map.Dirt )
				material.Set( $"g_t{name}", Texture.White );
			material.Set( $"g_v{name}Sampler", new Vector2( binding.SamplerIndex, binding.MirrorV ) );
		}

		boundNormals.x = textures[(int)UnitySnowMaterial.Map.BaseNormal].Path is not null ? 1 : 0;
		boundNormals.y = textures[(int)UnitySnowMaterial.Map.CoverNormal].Path is not null ? 1 : 0;
		material.Set( "g_vNormalMaps", boundNormals );
		material.Set( "g_vBaseST", source.Layers[(int)UnitySnowMaterial.Map.Base].Transform );
		material.Set( "g_vCoverST", source.Layers[(int)UnitySnowMaterial.Map.Cover].Transform );
		material.Set( "g_vNormalST", source.Layers[(int)UnitySnowMaterial.Map.BaseNormal].Transform );
		material.Set( "g_vTint", new Vector4( source.Tint.r, source.Tint.g, source.Tint.b, source.Tint.a ) );
		material.Set( "g_vSmoothness", source.Smoothness );
		material.Set( "g_flBias", source.Bias );
		material.Set( "g_flBlendDepth", source.BlendDepth );
		material.Set( "g_flNormalStrength", source.NormalStrength );
		material.Set( "g_flFrequency", source.Frequency );
		material.Set( "g_flFade", source.Fade );
		material.Set( "g_vDirtST", source.Layers[(int)UnitySnowMaterial.Map.Dirt].Transform );
		material.Set( "g_flDirtBias", source.DirtBias );
		material.Set( "g_flDirtSmoothness", source.DirtSmoothness );
		material.Set( "g_bDirtNormal", textures[(int)UnitySnowMaterial.Map.DirtNormal].Path is not null );
		material.SetFeature( "F_DIRT_LAYER", source.Dirt ? 1 : 0 );
		material.SetFeature( "F_TRIPLANAR", source.Triplanar ? 1 : 0 );
		material.SetFeature( "F_BINARY_BLEND", source.BinaryBlend ? 1 : 0 );
		BindMicrofacets( material, noise );
		return material;
	}

	internal static void BindMicrofacets( Material material, UnityMountedTexture noise )
	{
		if ( noise.Path is not null && Texture.Load( noise.Path ) is { IsError: false } texture )
		{
			material.Set( "g_tMicrofacetNoise", texture );
			material.Set( "g_vNoiseSampler", new Vector2( noise.SamplerIndex, noise.MirrorV ) );
			material.SetFeature( "F_MICROFACET_SNOW", 1 );
		}
	}
}

namespace UnityMount;

sealed class UnityOptimizedStandardMaterialLoader( UnityOptimizedStandardMaterial source, UnityMountedTexture[] layers ) : ResourceLoader<GameMount>
{
	protected override object Load()
	{
		var material = Material.Create( Path, "shaders/unity/optimized_standard.shader" );
		for ( var i = 0; i < layers.Length; i++ )
		{
			var map = (UnityOptimizedStandardMaterial.Map)i;
			var layer = layers[i];
			var fallback = map is UnityOptimizedStandardMaterial.Map.Green or UnityOptimizedStandardMaterial.Map.FlowRamp
				? Texture.Black : Texture.White;
			material.Set( $"g_t{map}", layer.Path is null ? fallback : Texture.Load( layer.Path ) );
			material.Set( $"g_v{map}Sampler", new Vector2( layer.SamplerIndex, layer.MirrorV ) );
		}

		material.Set( "g_vColorST", source.Layers[(int)UnityOptimizedStandardMaterial.Map.Color].Transform );
		material.Set( "g_vEmissionST", source.Layers[(int)UnityOptimizedStandardMaterial.Map.Emission].Transform );
		material.Set( "g_vSplatST", source.Layers[(int)UnityOptimizedStandardMaterial.Map.Splat].Transform );
		material.Set( "g_vTint", source.Tint );
		material.Set( "g_vEmissionTint", source.EmissionTint );
		material.Set( "g_vNormalTiling", source.NormalTiling );
		material.Set( "g_flNormalStrength", source.NormalStrength );
		material.Set( "g_flAlbedoMultiplier", source.AlbedoMultiplier );
		material.Set( "g_flSmoothness", source.Smoothness );
		material.Set( "g_bHasNormal", layers[(int)UnityOptimizedStandardMaterial.Map.Normal].Path is not null ? 1f : 0f );
		material.Set( "g_bHasFlowNormal", layers[(int)UnityOptimizedStandardMaterial.Map.FlowNormal].Path is not null ? 1f : 0f );
		material.Set( "g_bFresnel", source.Fresnel ? 1f : 0f );
		material.Set( "g_bSplat", source.Splat ? 1f : 0f );
		material.Set( "g_bVertexSplat", source.VertexSplat ? 1f : 0f );
		material.Set( "g_bFlow", source.Flow ? 1f : 0f );
		material.Set( "g_vFresnelSettings", source.FresnelSettings );
		material.Set( "g_vSplatSettings", source.SplatSettings );
		material.Set( "g_vFlowSettings", source.FlowSettings );
		material.Set( "g_flFlowEmission", source.FlowEmission );
		return material;
	}
}

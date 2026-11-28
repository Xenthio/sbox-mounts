namespace UnityMount;

sealed class UnitySolidParallaxMaterialLoader( UnitySolidParallaxMaterial source, UnityMountedTexture[] layers ) : ResourceLoader<GameMount>
{
	protected override object Load()
	{
		var material = Material.Create( Path, "shaders/unity/solid_parallax.shader" );
		for ( var i = 0; i < layers.Length; i++ )
		{
			var name = ((UnitySolidParallaxMaterial.Map)i).ToString();
			var layer = layers[i];
			var fallback = i == (int)UnitySolidParallaxMaterial.Map.Emission ? Texture.Black : Texture.White;
			material.Set( $"g_t{name}", layer.Path is null ? fallback : Texture.Load( layer.Path ) );
			material.Set( $"g_v{name}ST", source.Layers[i].Transform );
			material.Set( $"g_v{name}Sampler", new Vector2( layer.SamplerIndex, layer.MirrorV ) );
		}
		material.SetFeature( "F_CULL_MODE", source.Cull );
		material.Set( "g_vTint", new Vector3( source.Tint.r, source.Tint.g, source.Tint.b ) );
		material.Set( "g_vScroll", source.Scroll );
		material.Set( "g_vHeight", new Vector2( source.HeightStrength, source.HeightBias ) );
		material.Set( "g_flParallax", source.Parallax );
		material.Set( "g_flEmissionPower", source.EmissionPower );
		material.Set( "g_flSmoothness", source.Smoothness );
		material.Set( "g_bAlphaClip", source.AlphaClip ? 1f : 0f );
		material.Set( "g_bHasNormal", layers[(int)UnitySolidParallaxMaterial.Map.Normal].Path is not null ? 1f : 0f );
		return material;
	}
}

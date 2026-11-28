namespace UnityMount;

sealed class UnityCloudMaterialLoader( UnityCloudMaterial source, UnityMountedTexture main, UnityMountedTexture first,
	UnityMountedTexture second, UnityMountedTexture remap ) : ResourceLoader<GameMount>
{
	protected override object Load()
	{
		var material = Material.Create( Path, "shaders/unity/cloud_remap.shader" );
		if ( main.Path is not null ) material.Set( "g_tColor", Texture.Load( main.Path ) );
		else if ( source.Variant != UnityCloudMaterial.CloudVariant.Standard ) material.Set( "g_tColor", Texture.White );
		// The cloud textures contribute only alpha. Unity's default gray is opaque.
		material.Set( "g_tCloudFirst", first.Path is null ? Texture.White : Texture.Load( first.Path ) );
		material.Set( "g_tCloudSecond", second.Path is null ? Texture.White : Texture.Load( second.Path ) );
		if ( remap.Path is not null ) material.Set( "g_tRemap", Texture.Load( remap.Path ) );
		else if ( source.Variant == UnityCloudMaterial.CloudVariant.Optimized ) material.Set( "g_tRemap", Texture.Black );
		material.Set( "g_vSamplerIndices", new Vector4( main.SamplerIndex, first.SamplerIndex, second.SamplerIndex, remap.SamplerIndex ) );
		material.Set( "g_vMirrorV", new Vector4( main.MirrorV, first.MirrorV, second.MirrorV, remap.MirrorV ) );
		material.SetFeature( "F_ADDITIVE_BLEND", source.Additive ? 1 : 0 );
		material.SetFeature( "F_CULL_MODE", source.Cull );
		material.SetFeature( "F_ALWAYS_DRAW", source.AlwaysDraw ? 1 : 0 );
		material.SetFeature( "F_CLOUD_VARIANT", (int)source.Variant );
		if ( source.Variant == UnityCloudMaterial.CloudVariant.Intersection )
		{
			material.Set( "g_vIntersectionStyle", source.IntersectionStyle );
			material.Set( "g_bTriplanar", source.Triplanar ? 1f : 0f );
		}
		else if ( source.Variant == UnityCloudMaterial.CloudVariant.Optimized )
		{
			material.Set( "g_vDistortedMainST", source.DistortedMainTransform );
			material.Set( "g_vTexCoordST", source.TexCoordTransform );
			material.Set( "g_flRemapFactor", source.RemapFactor );
		}
		material.Set( "g_vMainST", source.Main.Transform );
		material.Set( "g_vFirstST", source.First.Transform );
		material.Set( "g_vSecondST", source.Second.Transform );
		material.Set( "g_vScroll", source.Scroll );
		material.Set( "g_vTint", new Vector4( source.Tint.r, source.Tint.g, source.Tint.b, source.Tint.a ) );
		material.Set( "g_flBoost", source.Boost );
		material.Set( "g_flAlphaBoost", source.AlphaBoost );
		material.Set( "g_flAlphaBias", source.AlphaBias );
		material.Set( "g_flFade", source.Fade );
		material.Set( "g_flExternalAlpha", source.ExternalAlpha );
		material.Set( "g_flDistortion", source.Distortion );
		material.Set( "g_flInverseFade", source.InverseFade / UnityCoordinates.ToInches( 1 ) );
		material.Set( "g_flCloseFade", source.CloseFade );
		material.Set( "g_flDepthOffset", UnityCoordinates.ToInches( source.DepthOffset ) );
		material.Set( "g_flVertexOffset", UnityCoordinates.ToInches( source.VertexOffset ) );
		material.Set( "g_flFresnelPower", source.FresnelPower );
		material.Set( "g_bClouds", source.Clouds ? 1f : 0f );
		material.Set( "g_bDistort", source.Distort ? 1f : 0f );
		material.Set( "g_bVertexColor", source.VertexColor ? 1f : 0f );
		material.Set( "g_bVertexAlpha", source.VertexAlpha ? 1f : 0f );
		material.Set( "g_bDisableRemap", source.DisableRemap ? 1f : 0f );
		material.Set( "g_bSkyOnly", source.SkyOnly ? 1f : 0f );
		material.Set( "g_bUseUv1", source.UseUv1 ? 1f : 0f );
		return material;
	}
}

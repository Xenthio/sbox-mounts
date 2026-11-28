namespace UnityMount;

sealed class UnityOpaqueCloudMaterialLoader( UnityOpaqueCloudMaterial source, UnityMountedTexture main,
	UnityMountedTexture first, UnityMountedTexture second, UnityMountedTexture remap, UnityMountedTexture normal ) : ResourceLoader<GameMount>
{
	protected override object Load()
	{
		var material = Material.Create( Path, "shaders/unity/opaque_cloud.shader" );
		material.SetFeature( "F_OPTIMIZED", source.Optimized ? 1 : 0 );
		material.SetFeature( "F_CULL_MODE", source.Cull );
		material.Set( "g_tColor", main.Path is null ? Texture.White : Texture.Load( main.Path ) );
		material.Set( "g_tCloudFirst", first.Path is null ? Texture.White : Texture.Load( first.Path ) );
		material.Set( "g_tCloudSecond", second.Path is null ? Texture.White : Texture.Load( second.Path ) );
		if ( remap.Path is not null ) material.Set( "g_tRemap", Texture.Load( remap.Path ) );
		else if ( source.Optimized ) material.Set( "g_tRemap", Texture.Black );
		if ( normal.Path is not null ) material.Set( "g_tNormal", Texture.Load( normal.Path ) );
		material.Set( "g_vSamplerIndices", new Vector4( main.SamplerIndex, first.SamplerIndex, second.SamplerIndex, remap.SamplerIndex ) );
		material.Set( "g_vMirrorV", new Vector4( main.MirrorV, first.MirrorV, second.MirrorV, remap.MirrorV ) );
		material.Set( "g_vNormalSampler", new Vector4( normal.SamplerIndex, normal.MirrorV, 0, 0 ) );
		material.Set( "g_vMainST", source.Main.Transform );
		material.Set( "g_vFirstST", source.First.Transform );
		material.Set( "g_vSecondST", source.Second.Transform );
		material.Set( "g_vDistortedMainST", source.DistortedMain );
		material.Set( "g_vScroll", source.Scroll );
		material.Set( "g_vTint", RGBA( source.Tint ) );
		material.Set( "g_vEmission", RGBA( source.Emission ) );
		material.Set( "g_vFresnelTint", RGBA( source.FresnelTint ) );
		material.Set( "g_flCutoff", source.Cutoff );
		material.Set( "g_flAlphaBoost", source.AlphaBoost );
		material.Set( "g_flAlphaBias", source.AlphaBias );
		material.Set( "g_flExternalAlpha", source.ExternalAlpha );
		material.Set( "g_flBoost", source.Boost );
		material.Set( "g_flVertexInfluence", source.VertexInfluence );
		material.Set( "g_flNormalStrength", source.NormalStrength );
		material.Set( "g_flDistortion", source.Distortion );
		material.Set( "g_flRemapFactor", source.RemapFactor );
		material.Set( "g_flFresnelPower", source.FresnelPower );
		material.Set( "g_bClouds", source.Clouds ? 1f : 0f );
		material.Set( "g_bCloudNormal", source.CloudNormal ? 1f : 0f );
		material.Set( "g_bEmissionFromAlbedo", source.EmissionFromAlbedo ? 1f : 0f );
		material.Set( "g_bVertexAlpha", source.VertexAlpha ? 1f : 0f );
		material.Set( "g_bVertexColor", source.VertexColor ? 1f : 0f );
		material.Set( "g_bDisableRemap", source.DisableRemap ? 1f : 0f );
		material.Set( "g_bHasNormal", normal.Path is null ? 0f : 1f );
		material.Set( "g_bHasRemap", remap.Path is null ? 0f : 1f );
		return material;
	}

	static Vector4 RGBA( Color color ) => new( color.r, color.g, color.b, color.a );
}

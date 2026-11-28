namespace UnityMount;

sealed class UnityStandardMaterialLoader( UnityMaterial source, UnityMountedTexture color, UnityMountedTexture normal,
	UnityMountedTexture emission, UnityMountedTexture mask, UnityMountedTexture ramp, UnityMountedTexture[] layers ) : ResourceLoader<GameMount>
{
	protected override object Load()
	{
		var standard = source.Standard;
		var material = Material.Create( Path, "shaders/unity/standard_surface.shader" );
		Bind( "Color", color );
		Bind( "Normal", normal );
		Bind( "Emission", emission );
		if ( standard.Fresnel )
		{
			Bind( "FresnelMask", mask );
			Bind( "FresnelRamp", ramp );
			material.Set( "g_flFresnelPower", standard.Power );
			material.Set( "g_flFresnelBoost", standard.Boost );
			material.SetFeature( "F_FRESNEL", 1 );
		}
		material.Set( "g_bHasNormal", normal.Path is not null ? 1f : 0f );
		material.Set( "g_vBaseST", standard.BaseTransform );
		material.Set( "g_vNormalST", standard.NormalTransform );
		material.Set( "g_vColorTint", new Vector4( source.Tint.r, source.Tint.g, source.Tint.b, source.Tint.a ) );
		material.Set( "g_vEmissionTint", new Vector3( standard.EmissionTint.r, standard.EmissionTint.g, standard.EmissionTint.b ) );
		material.Set( "g_flEmissionPower", standard.EmissionPower );
		material.Set( "g_flNormalStrength", standard.NormalStrength );
		material.Set( "g_flSmoothness", standard.Smoothness );
		if ( standard.Dither )
		{
			material.Set( "g_flFade", standard.Fade );
			material.Set( "g_flFadeBias", standard.FadeBias );
		}
		material.SetFeature( "F_CUTOUT", standard.Cutout ? 1 : 0 );
		material.SetFeature( "F_CULL_MODE", standard.Cull );
		if ( standard.Flow )
		{
			Bind( "FlowMap", layers[(int)UnityStandardMaterial.Map.FlowMap] );
			Bind( "FlowHeight", layers[(int)UnityStandardMaterial.Map.FlowHeight] );
			Bind( "FlowRamp", layers[(int)UnityStandardMaterial.Map.FlowRamp] );
			material.Set( "g_bFlow", 1f );
			material.Set( "g_vFlowSettings", standard.FlowSettings );
			material.Set( "g_vHeightSettings", standard.HeightSettings );
		}
		if ( standard.Splat )
		{
			Bind( "Green", layers[(int)UnityStandardMaterial.Map.Green] );
			Bind( "Blue", layers[(int)UnityStandardMaterial.Map.Blue] );
			var greenNormal = layers[(int)UnityStandardMaterial.Map.GreenNormal];
			var blueNormal = layers[(int)UnityStandardMaterial.Map.BlueNormal];
			Bind( "GreenNormal", greenNormal );
			Bind( "BlueNormal", blueNormal );
			material.Set( "g_bSplat", 1f );
			material.Set( "g_vSplatSettings", standard.SplatSettings );
			material.Set( "g_vSplatSmoothness", standard.SplatSmoothness );
			material.Set( "g_vSplatNormals", new Vector2( greenNormal.Path is not null ? 1f : 0f, blueNormal.Path is not null ? 1f : 0f ) );
			if ( !standard.VertexColors )
			{
				Bind( "Splat", layers[(int)UnityStandardMaterial.Map.Splat] );
				material.Set( "g_bUseSplatTexture", 1f );
				material.Set( "g_vSplatST", standard.SplatTransform );
			}
		}
		if ( standard.Print is { } print )
		{
			Bind( "SliceAlpha", layers[(int)UnityStandardMaterial.Map.SliceAlpha], useShaderDefault: true );
			Bind( "PrintRamp", layers[(int)UnityStandardMaterial.Map.PrintRamp], useShaderDefault: true );
			material.Set( "g_bPrintCutoff", 1f );
			material.Set( "g_flPrintDirection", print.Direction );
			material.Set( "g_vPrintSlice", new Vector4( print.SliceHeight, print.BandHeight, print.AlphaDepth, print.Bias ) );
			material.Set( "g_vPrintTint", new Vector2( print.Boost, print.EmissionToAlbedo ) );
			material.Set( "g_vSliceAlphaST", print.AlphaTransform );
		}
		return material;

		void Bind( string name, UnityMountedTexture binding, bool useShaderDefault = false )
		{
			if ( binding.Path is not null ) material.Set( $"g_t{name}", Texture.Load( binding.Path ) );
			else if ( !useShaderDefault ) material.Set( $"g_t{name}", Texture.White );
			material.Set( $"g_v{name}Sampler", new Vector2( binding.SamplerIndex, binding.MirrorV ) );
		}
	}
}

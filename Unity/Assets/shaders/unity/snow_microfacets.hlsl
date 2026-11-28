#if S_MICROFACET_SNOW
	CreateInputTexture2D( MicrofacetNoise, Srgb, 8, "", "", "", Default4( 0, 0, 0, 1 ) );
	Texture2D g_tMicrofacetNoise < Channel( RGBA, Box( MicrofacetNoise ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	float2 g_vNoiseSampler < Default2( 0, 0 ); >;

	void ApplySnowMicrofacets( inout Material material, float2 unityUv, float3 toCamera, float cover )
	{
		float2 uv = unityUv * 3;
		if ( g_vNoiseSampler.y ) uv.y = abs( uv.y );
		float noise = g_tMicrofacetNoise.SampleBias( Bindless::GetSampler( asDynamicUniform( (int)g_vNoiseSampler.x ) ),
			float2( uv.x, 1 - uv.y ), -0.5 ).r;
		float facing = saturate( dot( material.Normal, normalize( toCamera ) ) );
		material.Albedo *= 1 + 4 * pow( noise, 10 ) * facing * cover;
		material.Emission = material.Albedo * ((1 - facing) * 0.02 * cover);
	}
#endif

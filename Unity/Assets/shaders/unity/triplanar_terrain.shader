HEADER
{
	Description = "Vertex-blended Unity triplanar terrain";
}

FEATURES
{
	#include "common/features.hlsl"
	Feature( F_OPTIMIZED, 0..1, "Optimized Unity terrain" );
	Feature( F_DOUBLE_SAMPLE, 0..1, "Secondary texture scale" );
	Feature( F_MICROFACET_SNOW, 0..1, "Snow microfacets" );
	Feature( F_CULL_MODE, 0..2 ( 0="Off", 1="Front", 2="Back" ), "Face culling" );
}

MODES
{
	Forward();
	Depth();
}

COMMON
{
	#include "common/shared.hlsl"
	StaticCombo( S_OPTIMIZED, F_OPTIMIZED, Sys( ALL ) );
	StaticCombo( S_DOUBLE_SAMPLE, F_DOUBLE_SAMPLE, Sys( ALL ) );
	StaticCombo( S_MICROFACET_SNOW, F_MICROFACET_SNOW, Sys( ALL ) );
	StaticCombo( S_CULL_MODE, F_CULL_MODE, Sys( ALL ) );
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
	float4 vColor : COLOR0 < Semantic( Color ); >;
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
	float3 vTerrainMask : TEXCOORD12;
	float2 vTerrainUv : TEXCOORD13;
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput v )
	{
		PixelInput i = ProcessVertex( v );
		i.vTerrainMask = v.vColor.rgb;
		i.vTerrainUv = float2( v.vTexCoord.x, 1 - v.vTexCoord.y );
		return FinalizeVertex( i );
	}
}

PS
{
	#include "common/pixel.hlsl"
	#include "shaders/unity/snow_microfacets.hlsl"

	#if S_CULL_MODE == 0
		RenderState( CullMode, NONE );
	#elif S_CULL_MODE == 1
		RenderState( CullMode, FRONT );
	#else
		RenderState( CullMode, BACK );
	#endif

	CreateInputTexture2D( RedTop, Srgb, 8, "", "", "", Default4( 1, 1, 1, 1 ) );
	CreateInputTexture2D( RedSide, Srgb, 8, "", "", "", Default4( 1, 1, 1, 1 ) );
	CreateInputTexture2D( Green, Srgb, 8, "", "", "", Default4( 1, 1, 1, 1 ) );
	CreateInputTexture2D( Blue, Srgb, 8, "", "", "", Default4( 1, 1, 1, 1 ) );
	CreateInputTexture2D( Splat, Linear, 8, "", "", "", Default4( 1, 1, 1, 1 ) );
	CreateInputTexture2D( NormalMap, Linear, 8, "", "", "", Default4( 0.5, 0.5, 1, 1 ) );
	Texture2D g_tRedTop < Channel( RGBA, Box( RedTop ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	Texture2D g_tRedSide < Channel( RGBA, Box( RedSide ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	Texture2D g_tGreen < Channel( RGBA, Box( Green ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	Texture2D g_tBlue < Channel( RGBA, Box( Blue ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	Texture2D g_tSplat < Channel( RGBA, Box( Splat ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	Texture2D g_tNormal < Channel( RGBA, Box( NormalMap ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	float4 g_vRedTopST < Default4( 1, 1, 0, 0 ); >;
	float4 g_vRedSideST < Default4( 1, 1, 0, 0 ); >;
	float4 g_vGreenST < Default4( 1, 1, 0, 0 ); >;
	float4 g_vBlueST < Default4( 1, 1, 0, 0 ); >;
	float4 g_vSplatST < Default4( 1, 1, 0, 0 ); >;
	float4 g_vNormalST < Default4( 1, 1, 0, 0 ); >;
	float2 g_vRedTopSampler < Default2( 0, 0 ); >;
	float2 g_vRedSideSampler < Default2( 0, 0 ); >;
	float2 g_vGreenSampler < Default2( 0, 0 ); >;
	float2 g_vBlueSampler < Default2( 0, 0 ); >;
	float2 g_vSplatSampler < Default2( 0, 0 ); >;
	float2 g_vNormalSampler < Default2( 0, 0 ); >;
	float3 g_vTint < Default3( 0.5, 0.5, 0.5 ); >;
	float g_flTintAlpha < Default( 1 ); >;
	float3 g_vBias < Default3( 0, 0, 0 ); >;
	float3 g_vSmoothness < Default3( 0, 0, 0 ); >;
	float g_flFrequency < Default( 1 ); >;
	float g_flBlendDepth < Default( 0.2 ); >;
	float g_flNormalStrength < Default( 1 ); >;
	float g_bVertexColors < Default( 0 ); >;
	float g_bMixVertexColors < Default( 0 ); >;
	float g_bAlphaMask < Default( 0 ); >;
	float g_bVerticalBias < Default( 0 ); >;
	float g_bHasNormal < Default( 0 ); >;

	float4 SampleLayer( Texture2D texture, float2 uv, float2 binding )
	{
		if ( binding.y ) uv.y = abs( uv.y );
		return texture.Sample( Bindless::GetSampler( asDynamicUniform( (int)binding.x ) ), float2( uv.x, 1 - uv.y ) );
	}

	float3 Layers( float3 normal )
	{
		float3 weight = abs( float3( -normal.y, normal.z, normal.x ) );
		return weight / max( dot( weight, 1 ), 0.0001 );
	}

	float4 Project( Texture2D texture, float3 position, float3 weight, float2 tiling, float2 binding )
	{
		return SampleLayer( texture, position.zy * tiling, binding ) * weight.x
			+ SampleLayer( texture, position.xz * tiling, binding ) * weight.y
			+ SampleLayer( texture, position.xy * tiling, binding ) * weight.z;
	}

	struct TerrainSample
	{
		float3 Albedo;
		float3 Layers;
		float Strength;
	};

	TerrainSample SampleTerrain( float3 position, float3 projection, float3 mask )
	{
		float4 red = SampleLayer( g_tRedSide, position.zy * g_vRedSideST.xy, g_vRedSideSampler ) * projection.x
			+ SampleLayer( g_tRedTop, position.xz * g_vRedTopST.xy, g_vRedTopSampler ) * projection.y
			+ SampleLayer( g_tRedSide, position.xy * g_vRedSideST.xy, g_vRedSideSampler ) * projection.z;
		red *= float4( g_vTint, g_flTintAlpha );
		float4 green = Project( g_tGreen, position, projection, g_vGreenST.xy, g_vGreenSampler );
		float4 blue = Project( g_tBlue, position, projection, g_vBlueST.xy, g_vBlueSampler );
		if ( g_bAlphaMask ) mask *= float3( red.a, green.a, blue.a );

		// Keep layers within the authored depth of the dominant layer, then quantize the blend.
		float peak = max( mask.x, max( mask.y, mask.z ) );
		float3 height = max( 0, mask - (peak - g_flBlendDepth) );
		float total = dot( height, 1 );
		float3 blend = total > 0 ? round( height / total * 2 ) * 0.5 : step( peak, mask );
		float sum = blend.x + blend.y + blend.z * 0.5;
		blend = sum > 0 ? blend / sum : step( peak, mask ) / max( dot( step( peak, mask ), 1 ), 1 );

		TerrainSample sample;
		sample.Albedo = red.rgb * blend.x + green.rgb * blend.y + blue.rgb * blend.z;
		sample.Layers = blend;
		sample.Strength = length( height );
		return sample;
	}

	#if S_OPTIMIZED
		float3 g_vOptimizedTiling < Default3( 1, 1, 1 ); >;
		float3 g_vOptimizedAlbedo < Default3( 1, 1, 1 ); >;
		float3 g_vVertexPower < Default3( 1, 1, 1 ); >;
		float4 g_vOptimizedUvST < Default4( 1, 1, 0, 0 ); >;

		float4 ProjectOptimized( Texture2D side, Texture2D top, float2 sideBinding, float2 topBinding,
			float3 position, float forwardWeight, float topWeight )
		{
			float4 firstSide = SampleLayer( side, position.yz, sideBinding );
			float4 secondSide = SampleLayer( side, position.xy, sideBinding );
			float4 upper = SampleLayer( top, position.xz, topBinding );
			return lerp( lerp( firstSide, secondSide, forwardWeight ), upper, topWeight );
		}

		float4 ShadeOptimized( PixelInput i, float3 position, float3 normal )
		{
			if ( DepthNormals::WantsDepthNormals() ) return DepthNormals::Output( normal );

			float forward = saturate( abs( normal.x ) );
			float up = normal.z;
			float top = saturate( pow( up + 0.14 * pow( max( 0, 1 - up ), 3.72 ), 2.5 ) );
			position *= 0.01 / 39.3700787;
			float4 red = ProjectOptimized( g_tRedSide, g_tRedTop, g_vRedSideSampler, g_vRedTopSampler, position * g_vOptimizedTiling.x, forward, top );
			float4 green = ProjectOptimized( g_tGreen, g_tGreen, g_vGreenSampler, g_vGreenSampler, position * g_vOptimizedTiling.y, forward, top );
			float4 blue = ProjectOptimized( g_tBlue, g_tBlue, g_vBlueSampler, g_vBlueSampler, position * g_vOptimizedTiling.z, forward, top );

			float2 uv = i.vTerrainUv * g_vOptimizedUvST.xy + g_vOptimizedUvST.zw;
			uv = uv * g_vSplatST.xy + g_vSplatST.zw;
			float3 mask = SampleLayer( g_tSplat, uv, g_vSplatSampler ).rgb;
			mask += g_vBias + pow( i.vTerrainMask, g_vVertexPower ) + (red.a + green.a + blue.a) * 0.33;
			float3 blend = step( 0.1, mask ) + step( 0.2, mask );

			Material m = Material::Init( i );
			m.Albedo = red.rgb * g_vTint * (g_vOptimizedAlbedo.x * blend.x)
				+ green.rgb * (g_vOptimizedAlbedo.y * blend.y) + blue.rgb * (g_vOptimizedAlbedo.z * blend.z);
			m.Normal = normal;
			m.Roughness = 1 - saturate( dot( mask, g_vSmoothness ) );
			return ShadingModelStandard::Shade( m );
		}
	#endif

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		float3 world = i.vPositionWithOffsetWs + g_vCameraPositionWs;
		float3 position = float3( -world.y, world.z, world.x );
		float3 normal = normalize( i.vNormalWs );
		#if S_OPTIMIZED
			return ShadeOptimized( i, position, normal );
		#endif

		position *= g_flFrequency / 39.3700787;
		if ( g_bHasNormal )
		{
			float3 detail = DecodeNormal( Project( g_tNormal, position, Layers( normal ), g_vNormalST.xy, g_vNormalSampler ).rgb );
			detail = lerp( float3( 0, 0, 1 ), detail, g_flNormalStrength );
			normal = normalize( TransformNormal( detail, normal, i.vTangentUWs, i.vTangentVWs ) );
		}
		if ( DepthNormals::WantsDepthNormals() ) return DepthNormals::Output( normal );

		float3 mask = i.vTerrainMask;
		if ( g_bMixVertexColors || !g_bVertexColors )
		{
			float2 uv = i.vTerrainUv * g_vSplatST.xy + g_vSplatST.zw;
			float3 splat = SampleLayer( g_tSplat, uv, g_vSplatSampler ).rgb;
			mask = g_bMixVertexColors ? (splat + mask) * 0.5 : splat;
		}
		mask += g_vBias;
		if ( g_bVerticalBias ) mask.y += saturate( normal.z );

		float3 projection = Layers( normal );
		TerrainSample sample = SampleTerrain( position, projection, mask );
		float3 albedo = sample.Albedo;
		float3 blend = sample.Layers;
		#if S_DOUBLE_SAMPLE
			TerrainSample coarse = SampleTerrain( position * 0.2, projection, mask );
			float fine = sample.Strength / max( sample.Strength + coarse.Strength, 0.0001 );
			albedo = lerp( coarse.Albedo, albedo, fine );
			blend = lerp( coarse.Layers, blend, fine );
		#endif
		Material m = Material::Init( i );
		m.Albedo = albedo;
		m.Normal = normal;
		m.Roughness = 1 - saturate( dot( blend, g_vSmoothness ) );
		m.Metalness = 0;
		m.Opacity = 1;
		#if S_MICROFACET_SNOW
			ApplySnowMicrofacets( m, position.xz * g_vGreenST.xy, -i.vPositionWithOffsetWs, blend.y );
		#endif
		return ShadingModelStandard::Shade( m );
	}
}

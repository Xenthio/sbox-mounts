HEADER
{
	Description = "Unity slope-blended snow and moss surfaces";
}

FEATURES
{
	#include "common/features.hlsl"
	Feature( F_TRIPLANAR, 0..1, "World-projected cover" );
	Feature( F_DIRT_LAYER, 0..1, "Vertex-masked dirt layer" );
	Feature( F_BINARY_BLEND, 0..1, "Quantized height blend" );
	Feature( F_MICROFACET_SNOW, 0..1, "Snow microfacets" );
}

MODES
{
	Forward();
	Depth();
}

COMMON
{
	#include "common/shared.hlsl"
	StaticCombo( S_TRIPLANAR, F_TRIPLANAR, Sys( ALL ) );
	StaticCombo( S_DIRT_LAYER, F_DIRT_LAYER, Sys( ALL ) );
	StaticCombo( S_BINARY_BLEND, F_BINARY_BLEND, Sys( ALL ) );
	StaticCombo( S_MICROFACET_SNOW, F_MICROFACET_SNOW, Sys( ALL ) );
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
	float4 vColor : COLOR0 < Semantic( Color ); >;
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
	float2 vUnityUv : TEXCOORD12;
	float flDirtMask : TEXCOORD13;
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput v )
	{
		PixelInput i = ProcessVertex( v );
		i.vUnityUv = float2( v.vTexCoord.x, 1 - v.vTexCoord.y );
		i.flDirtMask = v.vColor.b;
		return FinalizeVertex( i );
	}
}

PS
{
	#include "common/pixel.hlsl"
	#include "shaders/unity/dither.hlsl"
	#include "shaders/unity/snow_microfacets.hlsl"

	CreateInputTexture2D( Base, Srgb, 8, "", "", "", Default4( 1, 1, 1, 1 ) );
	CreateInputTexture2D( Cover, Srgb, 8, "", "", "", Default4( 1, 1, 1, 1 ) );
	CreateInputTexture2D( BaseNormal, Linear, 8, "", "", "", Default4( 0.5, 0.5, 1, 1 ) );
	CreateInputTexture2D( CoverNormal, Linear, 8, "", "", "", Default4( 0.5, 0.5, 1, 1 ) );
	CreateInputTexture2D( Dirt, Srgb, 8, "", "", "", Default4( 1, 1, 1, 1 ) );
	CreateInputTexture2D( DirtNormal, Linear, 8, "", "", "", Default4( 0.5, 0.5, 1, 1 ) );
	Texture2D g_tDirt < Channel( RGBA, Box( Dirt ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	Texture2D g_tDirtNormal < Channel( RGBA, Box( DirtNormal ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	Texture2D g_tBase < Channel( RGBA, Box( Base ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	Texture2D g_tCover < Channel( RGBA, Box( Cover ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	Texture2D g_tBaseNormal < Channel( RGBA, Box( BaseNormal ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	Texture2D g_tCoverNormal < Channel( RGBA, Box( CoverNormal ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;

	float4 g_vDirtST < Default4( 1, 1, 0, 0 ); >;
	float2 g_vDirtSampler < Default2( 0, 0 ); >;
	float2 g_vDirtNormalSampler < Default2( 0, 0 ); >;
	float g_flDirtBias < Default( 0 ); >;
	float g_flDirtSmoothness < Default( 0 ); >;
	bool g_bDirtNormal < Default( 0 ); >;
	float4 g_vBaseST < Default4( 1, 1, 0, 0 ); >;
	float4 g_vCoverST < Default4( 1, 1, 0, 0 ); >;
	float4 g_vNormalST < Default4( 1, 1, 0, 0 ); >;
	float2 g_vBaseSampler < Default2( 0, 0 ); >;
	float2 g_vCoverSampler < Default2( 0, 0 ); >;
	float2 g_vBaseNormalSampler < Default2( 0, 0 ); >;
	float2 g_vCoverNormalSampler < Default2( 0, 0 ); >;
	float2 g_vNormalMaps < Default2( 0, 0 ); >;
	float4 g_vTint < Default4( 0.5, 0.5, 0.5, 1 ); >;
	float2 g_vSmoothness < Default2( 0, 0 ); >;
	float g_flBias < Default( 0 ); >;
	float g_flBlendDepth < Default( 0.2 ); >;
	float g_flNormalStrength < Default( 1 ); >;
	float g_flFrequency < Default( 1 ); >;
	float g_flFade < Default( 1 ); >;

	float4 SampleUnityTexture( Texture2D texture, float2 uv, float2 binding )
	{
		if ( binding.y ) uv.y = abs( uv.y );
		return texture.Sample( Bindless::GetSampler( asDynamicUniform( (int)binding.x ) ), float2( uv.x, 1 - uv.y ) );
	}

	float4 SampleCover( PixelInput i, float3 slopeNormal, Texture2D texture, float2 samplerBinding, float4 st )
	{
		#if S_TRIPLANAR
			float3 world = i.vPositionWithOffsetWs + g_vCameraPositionWs;
			float3 p = float3( -world.y, world.z, world.x ) * (g_flFrequency / 39.3700787);
			float3 weights = abs( float3( -slopeNormal.y, slopeNormal.z, slopeNormal.x ) );
			weights /= max( dot( weights, 1 ), 0.0001 );
			return SampleUnityTexture( texture, p.zy, samplerBinding ) * weights.x
				+ SampleUnityTexture( texture, p.xz, samplerBinding ) * weights.y
				+ SampleUnityTexture( texture, p.xy, samplerBinding ) * weights.z;
		#else
			float2 uv = i.vUnityUv * st.xy + st.zw;
			return SampleUnityTexture( texture, uv, samplerBinding );
		#endif
	}

	float2 HeightBlend( float slope, float baseAlpha, float coverAlpha )
	{
		float2 height = float2( 1 - slope + baseAlpha, slope + coverAlpha );
		float peak = max( height.x, height.y );
		float2 weights = max( 0, height - (peak - g_flBlendDepth) );
		float sum = weights.x + weights.y;
		weights = sum > 0 ? weights / sum : (height.y > height.x ? float2( 0, 1 ) : float2( 1, 0 ));
		#if S_BINARY_BLEND
			weights = round( weights * 2 ) * 0.5;
		#endif
		return weights;
	}

	float3 UnpackSurfaceNormal( float2 xy )
	{
		float z = sqrt( 1 - saturate( dot( xy, xy ) ) );
		return lerp( float3( 0, 0, 1 ), float3( xy, z ), g_flNormalStrength );
	}

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		ApplyUnityDither( i.vPositionSs.xy, g_flFade );

		float2 uv = i.vUnityUv * g_vBaseST.xy + g_vBaseST.zw;
		float2 normalUv = i.vUnityUv * g_vNormalST.xy + g_vNormalST.zw;
		float3 normal = normalize( i.vNormalWs );
		float2 baseNormal = g_vNormalMaps.x ? DecodeNormal( SampleUnityTexture( g_tBaseNormal, normalUv, g_vBaseNormalSampler ).rgb ).xy : 0;
		// The underlying normal map changes where the cover accumulates.
		float3 slopeNormal = TransformNormal( UnpackSurfaceNormal( baseNormal ), normal, i.vTangentUWs, i.vTangentVWs );
		float slope = saturate( slopeNormal.z + g_flBias );
		float4 base = SampleUnityTexture( g_tBase, uv, g_vBaseSampler ) * g_vTint;
		float4 cover = SampleCover( i, slopeNormal, g_tCover, g_vCoverSampler, g_vCoverST );
		float2 blend = HeightBlend( slope, base.a, cover.a );
		// The Unity shader uses the base normal UV transform for both normal textures.
		float2 coverNormal = g_vNormalMaps.y ? DecodeNormal( SampleUnityTexture( g_tCoverNormal, normalUv, g_vCoverNormalSampler ).rgb ).xy : 0;
		float2 layerNormal = baseNormal * blend.x + coverNormal * blend.y;
		float3 albedo = base.rgb * blend.x + cover.rgb * blend.y;
		float smoothness = dot( blend, g_vSmoothness );
		#if S_DIRT_LAYER
			float4 dirt = SampleCover( i, slopeNormal, g_tDirt, g_vDirtSampler, g_vDirtST );
			float3 height = float3( 1 + base.a, slope + cover.a, saturate( i.flDirtMask + g_flDirtBias ) + dirt.a );
			float peak = max( height.x, max( height.y, height.z ) );
			float3 weight = max( 0, height - (peak - g_flBlendDepth) );
			float total = dot( weight, 1 );
			weight = total > 0 ? weight / total :
				(height.x == peak ? float3( 1, 0, 0 ) : height.y == peak ? float3( 0, 1, 0 ) : float3( 0, 0, 1 ));
			#if S_BINARY_BLEND
				float3 quantized = round( weight * 2 ) * 0.5;
				if ( dot( quantized, 1 ) > 0 ) weight = quantized / dot( quantized, 1 );
			#endif
			float2 dirtNormal = g_bDirtNormal ? DecodeNormal( SampleUnityTexture( g_tDirtNormal, normalUv, g_vDirtNormalSampler ).rgb ).xy : 0;
			layerNormal = baseNormal * weight.x + coverNormal * weight.y + dirtNormal * weight.z;
			albedo = base.rgb * weight.x + cover.rgb * weight.y + dirt.rgb * weight.z;
			smoothness = dot( weight, float3( g_vSmoothness, g_flDirtSmoothness ) );
			blend = weight.xy;
		#endif
		float3 detail = UnpackSurfaceNormal( layerNormal );
		float3 shadingNormal = normalize( TransformNormal( detail, normal, i.vTangentUWs, i.vTangentVWs ) );
		if ( DepthNormals::WantsDepthNormals() ) return DepthNormals::Output( shadingNormal );

		Material m = Material::Init( i );
		m.Albedo = albedo;
		m.Normal = shadingNormal;
		m.Roughness = 1 - saturate( smoothness );
		#if S_MICROFACET_SNOW
			ApplySnowMicrofacets( m, i.vUnityUv * g_vCoverST.xy + g_vCoverST.zw, -i.vPositionWithOffsetWs, blend.y );
		#endif
		return ShadingModelStandard::Shade( m );
	}
}

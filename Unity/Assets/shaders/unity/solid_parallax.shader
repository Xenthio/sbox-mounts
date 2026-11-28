HEADER
{
	Description = "Unity solid parallax surfaces";
}

FEATURES
{
	#include "common/features.hlsl"
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
	float2 vUnityUv : TEXCOORD12;
	float flMaskAlpha : TEXCOORD13;
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput v )
	{
		PixelInput i = ProcessVertex( v );
		i.vUnityUv = float2( v.vTexCoord.x, 1 - v.vTexCoord.y );
		i.flMaskAlpha = v.vColor.a;
		return FinalizeVertex( i );
	}
}

PS
{
	#include "common/pixel.hlsl"

	#if S_CULL_MODE == 0
		RenderState( CullMode, NONE );
	#elif S_CULL_MODE == 1
		RenderState( CullMode, FRONT );
	#else
		RenderState( CullMode, BACK );
	#endif

	CreateInputTexture2D( Color, Srgb, 8, "", "", "", Default4( 1, 1, 1, 1 ) );
	CreateInputTexture2D( Emission, Srgb, 8, "", "", "", Default4( 0, 0, 0, 1 ) );
	CreateInputTexture2D( Normal, Linear, 8, "", "", "", Default4( 0.5, 0.5, 1, 1 ) );
	CreateInputTexture2D( Height1, Linear, 8, "", "", "", Default4( 1, 1, 1, 1 ) );
	CreateInputTexture2D( Height2, Linear, 8, "", "", "", Default4( 1, 1, 1, 1 ) );
	Texture2D g_tColor < Channel( RGBA, Box( Color ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	Texture2D g_tEmission < Channel( RGBA, Box( Emission ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	Texture2D g_tNormal < Channel( RGBA, Box( Normal ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	Texture2D g_tHeight1 < Channel( RGBA, Box( Height1 ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	Texture2D g_tHeight2 < Channel( RGBA, Box( Height2 ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	float4 g_vColorST < Default4( 1, 1, 0, 0 ); >;
	float4 g_vEmissionST < Default4( 1, 1, 0, 0 ); >;
	float4 g_vNormalST < Default4( 1, 1, 0, 0 ); >;
	float4 g_vHeight1ST < Default4( 1, 1, 0, 0 ); >;
	float4 g_vHeight2ST < Default4( 1, 1, 0, 0 ); >;
	float2 g_vColorSampler < Default2( 0, 0 ); >;
	float2 g_vEmissionSampler < Default2( 0, 0 ); >;
	float2 g_vNormalSampler < Default2( 0, 0 ); >;
	float2 g_vHeight1Sampler < Default2( 0, 0 ); >;
	float2 g_vHeight2Sampler < Default2( 0, 0 ); >;
	float3 g_vTint < Default3( 1, 1, 1 ); >;
	float4 g_vScroll < Default4( 0, 0, 0, 0 ); >;
	float2 g_vHeight < Default2( 1, 0 ); >;
	float g_flParallax < Default( 0 ); >;
	float g_flEmissionPower < Default( 1 ); >;
	float g_flSmoothness < Default( 0 ); >;
	float g_bAlphaClip < Default( 0 ); >;
	float g_bHasNormal < Default( 0 ); >;

	float4 SampleLayer( Texture2D texture, float2 uv, float4 st, float2 offset, float2 binding )
	{
		uv = uv * st.xy + st.zw + offset;
		if ( binding.y ) uv.y = abs( uv.y );
		return texture.Sample( Bindless::GetSampler( asDynamicUniform( (int)binding.x ) ), float2( uv.x, 1 - uv.y ) );
	}

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		float4 scroll = g_vScroll * (g_flTime / 20);
		float first = SampleLayer( g_tHeight1, i.vUnityUv, g_vHeight1ST, scroll.xy, g_vHeight1Sampler ).r;
		float second = SampleLayer( g_tHeight2, i.vUnityUv, g_vHeight2ST, scroll.zw, g_vHeight2Sampler ).r;
		float height = first * second * g_vHeight.x + g_vHeight.y;
		if ( g_bAlphaClip ) clip( height * i.flMaskAlpha - 0.5 );

		float3 normal = normalize( i.vNormalWs );
		float3 view = normalize( -i.vPositionWithOffsetWs );
		float divisor = dot( view, normal ) + 0.42;
		divisor = abs( divisor ) < 0.0001 ? (divisor < 0 ? -0.0001 : 0.0001) : divisor;
		// Source's tangent Y follows the flipped mesh V coordinate.
		float2 tangentView = float2( dot( view, i.vTangentUWs ), -dot( view, i.vTangentVWs ) );
		float2 offset = tangentView * ((height - 0.5) * g_flParallax / divisor);
		if ( g_bHasNormal )
		{
			float4 encoded = SampleLayer( g_tNormal, i.vUnityUv, g_vNormalST, offset, g_vNormalSampler );
			float2 xy = float2( encoded.r * encoded.a, encoded.g ) * 2 - 1;
			float3 detail = float3( xy.x, -xy.y, sqrt( saturate( 1 - dot( xy, xy ) ) ) );
			normal = normalize( TransformNormal( detail, normal, i.vTangentUWs, i.vTangentVWs ) );
		}
		if ( DepthNormals::WantsDepthNormals() ) return DepthNormals::Output( normal );

		float3 emission = SampleLayer( g_tEmission, i.vUnityUv, g_vEmissionST, offset, g_vEmissionSampler ).rgb * g_vTint;
		Material m = Material::Init( i );
		m.Albedo = SampleLayer( g_tColor, i.vUnityUv, g_vColorST, offset, g_vColorSampler ).rgb * g_vTint;
		m.Emission = pow( max( emission, 0 ), g_flEmissionPower );
		m.Normal = normal;
		m.Roughness = 1 - saturate( g_flSmoothness );
		return ShadingModelStandard::Shade( m );
	}
}

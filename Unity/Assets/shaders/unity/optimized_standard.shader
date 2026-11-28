HEADER
{
	Description = "Unity optimized environment surfaces";
}

FEATURES
{
	#include "common/features.hlsl"
}

MODES
{
	Forward();
	Depth();
}

COMMON
{
	#include "common/shared.hlsl"
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
	float flVertexSplat : TEXCOORD13;
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput v )
	{
		PixelInput i = ProcessVertex( v );
		i.vUnityUv = float2( v.vTexCoord.x, 1 - v.vTexCoord.y );
		i.flVertexSplat = v.vColor.g;
		return FinalizeVertex( i );
	}
}

PS
{
	#include "common/pixel.hlsl"

	CreateInputTexture2D( Color, Srgb, 8, "", "", "", Default4( 1, 1, 1, 1 ) );
	CreateInputTexture2D( Normal, Linear, 8, "", "", "", Default4( 0.5, 0.5, 1, 1 ) );
	CreateInputTexture2D( Emission, Srgb, 8, "", "", "", Default4( 1, 1, 1, 1 ) );
	CreateInputTexture2D( Ramp, Srgb, 8, "", "", "", Default4( 1, 1, 1, 1 ) );
	CreateInputTexture2D( Splat, Linear, 8, "", "", "", Default4( 1, 1, 1, 1 ) );
	CreateInputTexture2D( Green, Srgb, 8, "", "", "", Default4( 0, 0, 0, 1 ) );
	CreateInputTexture2D( Blue, Srgb, 8, "", "", "", Default4( 1, 1, 1, 1 ) );
	CreateInputTexture2D( Flow, Linear, 8, "", "", "", Default4( 1, 1, 1, 1 ) );
	CreateInputTexture2D( FlowNormal, Linear, 8, "", "", "", Default4( 0.5, 0.5, 1, 1 ) );
	CreateInputTexture2D( FlowHeight, Linear, 8, "", "", "", Default4( 1, 1, 1, 1 ) );
	CreateInputTexture2D( FlowRamp, Srgb, 8, "", "", "", Default4( 0, 0, 0, 1 ) );
	Texture2D g_tColor < Channel( RGBA, Box( Color ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	Texture2D g_tNormal < Channel( RGBA, Box( Normal ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	Texture2D g_tEmission < Channel( RGBA, Box( Emission ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	Texture2D g_tRamp < Channel( RGBA, Box( Ramp ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	Texture2D g_tSplat < Channel( RGBA, Box( Splat ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	Texture2D g_tGreen < Channel( RGBA, Box( Green ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	Texture2D g_tBlue < Channel( RGBA, Box( Blue ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	Texture2D g_tFlow < Channel( RGBA, Box( Flow ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	Texture2D g_tFlowNormal < Channel( RGBA, Box( FlowNormal ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	Texture2D g_tFlowHeight < Channel( RGBA, Box( FlowHeight ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	Texture2D g_tFlowRamp < Channel( RGBA, Box( FlowRamp ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	float2 g_vColorSampler < Default2( 0, 0 ); >;
	float2 g_vNormalSampler < Default2( 0, 0 ); >;
	float2 g_vEmissionSampler < Default2( 0, 0 ); >;
	float2 g_vRampSampler < Default2( 0, 0 ); >;
	float2 g_vSplatSampler < Default2( 0, 0 ); >;
	float2 g_vGreenSampler < Default2( 0, 0 ); >;
	float2 g_vBlueSampler < Default2( 0, 0 ); >;
	float2 g_vFlowSampler < Default2( 0, 0 ); >;
	float2 g_vFlowNormalSampler < Default2( 0, 0 ); >;
	float2 g_vFlowHeightSampler < Default2( 0, 0 ); >;
	float2 g_vFlowRampSampler < Default2( 0, 0 ); >;

	float4 g_vColorST < Default4( 1, 1, 0, 0 ); >;
	float4 g_vEmissionST < Default4( 1, 1, 0, 0 ); >;
	float4 g_vSplatST < Default4( 1, 1, 0, 0 ); >;
	float3 g_vTint < Default3( 1, 1, 1 ); >;
	float3 g_vEmissionTint < Default3( 0, 0, 0 ); >;
	float2 g_vNormalTiling < Default2( 1, 1 ); >;
	float g_flNormalStrength < Default( 1 ); >;
	float g_flAlbedoMultiplier < Default( 2.6 ); >;
	float g_flSmoothness < Default( 0 ); >;
	float g_bHasNormal < Default( 0 ); >;
	float g_bHasFlowNormal < Default( 0 ); >;
	float g_bFresnel < Default( 0 ); >;
	float g_bSplat < Default( 0 ); >;
	float g_bVertexSplat < Default( 0 ); >;
	float g_bFlow < Default( 0 ); >;
	float4 g_vFresnelSettings < Default4( 1, 5, 0, 0 ); >;
	float4 g_vSplatSettings < Default4( 0, 0, 0, 0 ); >;
	float4 g_vFlowSettings < Default4( 3, 0, 0, 1 ); >;
	float g_flFlowEmission < Default( 1 ); >;

	float4 SampleLayer( Texture2D texture, float2 uv, float2 binding )
	{
		if ( binding.y ) uv.y = abs( uv.y );
		return texture.Sample( Bindless::GetSampler( asDynamicUniform( (int)binding.x ) ), float2( uv.x, 1 - uv.y ) );
	}

	float3 BlendSplat( float3 albedo, float2 uv, float vertexMask )
	{
		float mask = g_bVertexSplat ? vertexMask : SampleLayer( g_tSplat, uv * g_vSplatST.xy + g_vSplatST.zw, g_vSplatSampler ).g;
		float2 tile = uv * g_vSplatSettings.x;
		float4 green = SampleLayer( g_tGreen, tile, g_vGreenSampler );
		float4 blue = SampleLayer( g_tBlue, tile, g_vBlueSampler );
		float greenWeight = green.a * mask > 0.7 - 0.2 * g_vSplatSettings.y ? 1 : 0;
		float blueWeight = blue.a * mask > 0.7 - 0.2 * g_vSplatSettings.z ? 1 : 0;
		return lerp( albedo, saturate( lerp( green.rgb, blue.rgb, blueWeight ) ), (greenWeight + blueWeight) * g_vSplatSettings.w );
	}

	float3 FlowSurface( float2 uv, inout float3 tangentNormal )
	{
		float2 flowUv = uv * g_vFlowSettings.x + float2( g_flTime * g_vFlowSettings.y * 0.01, 0 );
		float flow = SampleLayer( g_tFlow, flowUv, g_vFlowSampler ).r;
		float bias = g_vFlowSettings.z;
		float height = SampleLayer( g_tFlowHeight, uv + flow * bias * 0.1, g_vFlowHeightSampler ).r;
		if ( g_bHasFlowNormal )
		{
			float4 encoded = SampleLayer( g_tFlowNormal, flowUv, g_vFlowNormalSampler );
			tangentNormal += (encoded.r + encoded.a - 1) * bias;
		}
		float rampUv = height * g_vFlowSettings.w;
		return SampleLayer( g_tFlowRamp, rampUv.xx, g_vFlowRampSampler ).rgb * g_flFlowEmission;
	}

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		float2 uv = i.vUnityUv;
		float3 geometricNormal = normalize( i.vNormalWs );
		float3 tangentNormal = float3( 0, 0, 1 );
		if ( g_bHasNormal )
		{
			float4 encoded = SampleLayer( g_tNormal, uv * g_vNormalTiling, g_vNormalSampler );
			float2 xy = (float2( encoded.r * encoded.a, encoded.g ) * 2 - 1) * g_flNormalStrength;
			tangentNormal = float3( xy, sqrt( saturate( 1 - dot( xy, xy ) ) ) );
		}
		float3 flowEmission = 0;
		if ( g_bFlow ) flowEmission = FlowSurface( uv, tangentNormal );

		// The mesh importer flips V, reversing the corresponding tangent axis.
		tangentNormal.y = -tangentNormal.y;
		float3 normal = normalize( TransformNormal( tangentNormal, geometricNormal, i.vTangentUWs, i.vTangentVWs ) );
		if ( DepthNormals::WantsDepthNormals() ) return DepthNormals::Output( normal );

		float4 color = SampleLayer( g_tColor, uv * g_vColorST.xy + g_vColorST.zw, g_vColorSampler );
		float3 albedo = saturate( color.rgb * g_vTint * g_flAlbedoMultiplier );
		if ( g_bSplat ) albedo = BlendSplat( albedo, uv, i.flVertexSplat );
		if ( g_bFresnel )
		{
			float facing = dot( geometricNormal, normalize( -i.vPositionWithOffsetWs ) );
			float grazing = pow( max( 1 - facing, 0 ), g_vFresnelSettings.y );
			float rampUv = 1 - saturate( grazing * g_vFresnelSettings.x ) + color.a * g_vFresnelSettings.z;
			albedo += saturate( SampleLayer( g_tRamp, rampUv.xx, g_vRampSampler ).rgb * g_vFresnelSettings.w );
		}

		Material m = Material::Init( i );
		m.Albedo = albedo;
		m.Normal = normal;
		m.Roughness = 1 - saturate( g_flSmoothness );
		m.Emission = SampleLayer( g_tEmission, uv * g_vEmissionST.xy + g_vEmissionST.zw, g_vEmissionSampler ).rgb
			* g_vEmissionTint + flowEmission;
		return ShadingModelStandard::Shade( m );
	}
}

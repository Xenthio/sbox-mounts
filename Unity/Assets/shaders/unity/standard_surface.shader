HEADER
{
	Description = "Unity Standard surfaces with independent normal mapping and optional Fresnel";
}

FEATURES
{
	#include "common/features.hlsl"
	Feature( F_FRESNEL, 0..1, "View-dependent color ramp" );
	Feature( F_CUTOUT, 0..1, "Alpha cutout" );
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
	StaticCombo( S_FRESNEL, F_FRESNEL, Sys( ALL ) );
	StaticCombo( S_CUTOUT, F_CUTOUT, Sys( ALL ) );
	StaticCombo( S_CULL_MODE, F_CULL_MODE, Sys( ALL ) );
	float g_bPrintCutoff < Default( 0 ); >;
	float g_flPrintDirection < Default( 0 ); >;
	// Height, bias, override state (-1 off, 0 material, 1 on), flow power (-1 material).
	float4 g_vUnityPrintState < Attribute( "UnityPrintState" ); Default4( 0, 0, 0, -1 ); >;

	bool IsPrintEnabled()
	{
		return g_vUnityPrintState.z == 0 ? g_bPrintCutoff != 0 : g_vUnityPrintState.z > 0;
	}
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
	float3 vSplatMask : TEXCOORD13;
	float2 vSliceUv : TEXCOORD14;
	float flPrintPosition : TEXCOORD15;
	#if PROGRAM == VFX_PROGRAM_PS
		bool bIsFrontFace : SV_IsFrontFace;
	#endif
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput v )
	{
		PixelInput i = ProcessVertex( v );
		i.vUnityUv = float2( v.vTexCoord.x, 1 - v.vTexCoord.y );
		i.vSplatMask = v.vColor.rgb;
		i.vSliceUv = float2( v.vTexCoord2.x, 1 - v.vTexCoord2.y );
		i.flPrintPosition = 0;
		if ( IsPrintEnabled() )
		{
			float3x4 transform = GetTransformMatrix( v.nInstanceTransformID );
			float3 delta = mul( transform, float4( v.vPositionOs, 0 ) );
			float3 axis = mul( transform, g_flPrintDirection > 1.5 ? float4( 1, 0, 0, 0 ) : float4( 0, 0, 1, 0 ) );
			if ( g_flPrintDirection > 1.5 ) axis.z = 0;
			i.flPrintPosition = dot( delta, axis ) / max( dot( axis, axis ), 0.0001 ) / 39.3700787;
		}
		return FinalizeVertex( i );
	}
}

PS
{
	#include "common/pixel.hlsl"
	#include "shaders/unity/dither.hlsl"

	#if S_CULL_MODE == 0
		RenderState( CullMode, NONE );
	#elif S_CULL_MODE == 1
		RenderState( CullMode, FRONT );
	#else
		RenderState( CullMode, BACK );
	#endif

	CreateInputTexture2D( Color, Srgb, 8, "", "", "", Default4( 1, 1, 1, 1 ) );
	CreateInputTexture2D( Normal, Linear, 8, "", "", "", Default4( 0.5, 0.5, 1, 1 ) );
	CreateInputTexture2D( Emission, Srgb, 8, "", "", "", Default4( 1, 1, 1, 1 ) );
	CreateInputTexture2D( FresnelMask, Linear, 8, "", "", "", Default4( 1, 1, 1, 1 ) );
	CreateInputTexture2D( FresnelRamp, Srgb, 8, "", "", "", Default4( 1, 1, 1, 1 ) );
	Texture2D g_tColor < Channel( RGBA, Box( Color ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	Texture2D g_tNormal < Channel( RGBA, Box( Normal ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	Texture2D g_tEmission < Channel( RGBA, Box( Emission ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	Texture2D g_tFresnelMask < Channel( RGBA, Box( FresnelMask ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	Texture2D g_tFresnelRamp < Channel( RGBA, Box( FresnelRamp ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	CreateInputTexture2D( GreenLayer, Srgb, 8, "", "", "", Default4( 1, 1, 1, 1 ) );
	CreateInputTexture2D( BlueLayer, Srgb, 8, "", "", "", Default4( 1, 1, 1, 1 ) );
	CreateInputTexture2D( GreenNormal, Linear, 8, "", "", "", Default4( 0.5, 0.5, 1, 1 ) );
	CreateInputTexture2D( BlueNormal, Linear, 8, "", "", "", Default4( 0.5, 0.5, 1, 1 ) );
	CreateInputTexture2D( FlowMap, Linear, 8, "", "", "", Default4( 0.5, 0.5, 0, 1 ) );
	CreateInputTexture2D( FlowHeight, Linear, 8, "", "", "", Default4( 1, 1, 1, 1 ) );
	CreateInputTexture2D( FlowRamp, Srgb, 8, "", "", "", Default4( 1, 1, 1, 1 ) );
	CreateInputTexture2D( Splat, Linear, 8, "", "", "", Default4( 1, 1, 1, 1 ) );
	CreateInputTexture2D( SliceAlpha, Linear, 8, "", "", "", Default4( 0.5, 0.5, 0.5, 0.5 ) );
	CreateInputTexture2D( PrintRamp, Srgb, 8, "", "", "", Default4( 0.5, 0.5, 0.5, 0.5 ) );
	Texture2D g_tGreen < Channel( RGBA, Box( GreenLayer ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	Texture2D g_tBlue < Channel( RGBA, Box( BlueLayer ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	Texture2D g_tGreenNormal < Channel( RGBA, Box( GreenNormal ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	Texture2D g_tBlueNormal < Channel( RGBA, Box( BlueNormal ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	Texture2D g_tFlowMap < Channel( RGBA, Box( FlowMap ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	Texture2D g_tFlowHeight < Channel( RGBA, Box( FlowHeight ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	Texture2D g_tFlowRamp < Channel( RGBA, Box( FlowRamp ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	Texture2D g_tSplat < Channel( RGBA, Box( Splat ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	Texture2D g_tSliceAlpha < Channel( RGBA, Box( SliceAlpha ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	Texture2D g_tPrintRamp < Channel( RGBA, Box( PrintRamp ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;

	float4 g_vBaseST < Default4( 1, 1, 0, 0 ); >;
	float4 g_vNormalST < Default4( 1, 1, 0, 0 ); >;
	float2 g_vColorSampler < Default2( 0, 0 ); >;
	float2 g_vNormalSampler < Default2( 0, 0 ); >;
	float2 g_vEmissionSampler < Default2( 0, 0 ); >;
	float2 g_vFresnelMaskSampler < Default2( 0, 0 ); >;
	float2 g_vFresnelRampSampler < Default2( 0, 0 ); >;
	float4 g_vColorTint < Default4( 1, 1, 1, 1 ); >;
	float3 g_vEmissionTint < Default3( 0, 0, 0 ); >;
	float g_flEmissionPower < Default( 1 ); >;
	float g_flFresnelPower < Default( 1 ); >;
	float g_flFresnelBoost < Default( 1 ); >;
	float g_flNormalStrength < Default( 1 ); >;
	float g_flSmoothness < Default( 0 ); >;
	float g_flFade < Default( 1 ); >;
	float g_flFadeBias < Default( 0 ); >;
	float g_bHasNormal < Default( 0 ); >;
	float g_bFlow < Default( 0 ); >;
	float g_bSplat < Default( 0 ); >;
	float g_bUseSplatTexture < Default( 0 ); >;
	float4 g_vSplatST < Default4( 1, 1, 0, 0 ); >;
	float2 g_vSplatSampler < Default2( 0, 0 ); >;
	float4 g_vSliceAlphaST < Default4( 1, 1, 0, 0 ); >;
	float4 g_vPrintSlice < Default4( 5, 1, 0.1, 0 ); >;
	float2 g_vPrintTint < Default2( 1, 0 ); >;
	uint g_nUnityAnimatedMaterialChannels < Attribute( "UnityAnimatedMaterialChannels" ); Default( 0 ); >;
	float4 g_vUnityAnimatedPrintSlice < Attribute( "UnityAnimatedPrintSlice" ); Default4( 0, 0, 0, 0 ); >;
	float4 g_vUnityAnimatedSliceTransform < Attribute( "UnityAnimatedSliceTransform" ); Default4( 0, 0, 0, 0 ); >;
	float4 g_vUnityAnimatedEmissionTint < Attribute( "UnityAnimatedEmissionTint" ); Default4( 0, 0, 0, 0 ); >;
	float g_flUnityAnimatedPrintBoost < Attribute( "UnityAnimatedPrintBoost" ); Default( 0 ); >;
	float g_flUnityAnimatedEmissionPower < Attribute( "UnityAnimatedEmissionPower" ); Default( 0 ); >;
	float g_flUnityAnimatedFresnelBoost < Attribute( "UnityAnimatedFresnelBoost" ); Default( 0 ); >;
	float2 g_vSliceAlphaSampler < Default2( 0, 0 ); >;
	float2 g_vPrintRampSampler < Default2( 0, 0 ); >;
	float4 g_vFlowSettings < Default4( 1, 1, 1, 0 ); >;
	float3 g_vHeightSettings < Default3( 0, 1, 1 ); >;
	float4 g_vSplatSettings < Default4( 1, 0.2, 0, 0 ); >;
	float2 g_vSplatSmoothness < Default2( 0, 0 ); >;
	float2 g_vSplatNormals < Default2( 0, 0 ); >;
	float2 g_vGreenSampler < Default2( 0, 0 ); >;
	float2 g_vBlueSampler < Default2( 0, 0 ); >;
	float2 g_vGreenNormalSampler < Default2( 0, 0 ); >;
	float2 g_vBlueNormalSampler < Default2( 0, 0 ); >;
	float2 g_vFlowMapSampler < Default2( 0, 0 ); >;
	float2 g_vFlowHeightSampler < Default2( 0, 0 ); >;
	float2 g_vFlowRampSampler < Default2( 0, 0 ); >;

	float4 SampleLayer( Texture2D texture, float2 uv, float2 binding )
	{
		if ( binding.y ) uv.y = abs( uv.y );
		return texture.Sample( Bindless::GetSampler( asDynamicUniform( (int)binding.x ) ), float2( uv.x, 1 - uv.y ) );
	}

	float3 SplatWeights( float3 heights, float depth )
	{
		if ( depth <= 0.01 )
		{
			if ( heights.x > max( heights.y, heights.z ) ) return float3( 1, 0, 0 );
			if ( heights.y > heights.z ) return float3( 0, 1, 0 );
			return float3( 0, 0, 1 );
		}
		float peak = max( heights.x, max( heights.y, heights.z ) );
		float3 blend = saturate( heights - (peak - depth) );
		return blend / max( dot( blend, 1 ), 0.0001 );
	}

	float3 UnpackUnityNormal( float4 encoded )
	{
		float2 normal = float2( encoded.r * encoded.a, encoded.g ) * 2 - 1;
		float z = sqrt( saturate( 1 - dot( normal, normal ) ) );
		// Mesh V is flipped on import, so its tangent space reverses Y.
		return normalize( float3( normal.x, -normal.y, z ) );
	}

	float3 FlowSurface( float2 uv, out float4 encodedNormal )
	{
		float2 flow = SampleLayer( g_tFlowMap, uv * g_vFlowSettings.x, g_vFlowMapSampler ).rg;
		float2 direction = (flow * 2 - 1) * g_vFlowSettings.y;
		float first = frac( g_flTime * g_vFlowSettings.z / 20 );
		float second = frac( first + 0.5 );
		float blend = abs( first * 2 - 1 );
		float2 firstUv = uv + direction * first;
		float2 secondUv = uv + direction * second;
		encodedNormal = lerp( SampleLayer( g_tNormal, firstUv, g_vNormalSampler ),
			SampleLayer( g_tNormal, secondUv, g_vNormalSampler ), blend );
		float height = lerp( SampleLayer( g_tFlowHeight, firstUv, g_vFlowHeightSampler ).a,
			SampleLayer( g_tFlowHeight, secondUv, g_vFlowHeightSampler ).a - flow.x * g_vFlowSettings.w, blend );
		float power = g_vUnityPrintState.w >= 0 ? g_vUnityPrintState.w : g_vHeightSettings.y;
		float ramp = pow( saturate( height + g_vHeightSettings.x ), power );
		return SampleLayer( g_tFlowRamp, float2( ramp, 0.5 ), g_vFlowRampSampler ).rgb * g_vHeightSettings.z;
	}

	float4 AnimatedChannels( float4 original, float4 values, uint firstBit )
	{
		uint flags = g_nUnityAnimatedMaterialChannels >> firstBit;
		float4 mask = float4( (flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0, (flags & 8) != 0 );
		return lerp( original, values, mask );
	}

	void ApplyPrintCutoff( PixelInput i, inout float3 albedo, inout float3 emission )
	{
		float4 maskTransform = AnimatedChannels( g_vSliceAlphaST, g_vUnityAnimatedSliceTransform, 4 );
		float4 sliceSettings = AnimatedChannels( g_vPrintSlice, g_vUnityAnimatedPrintSlice, 0 );
		float2 uv = i.vSliceUv * maskTransform.xy + maskTransform.zw;
		float mask = SampleLayer( g_tSliceAlpha, uv, g_vSliceAlphaSampler ).a;
		float edge = 1 - mask * sliceSettings.z;
		float height = g_vUnityPrintState.z > 0 ? g_vUnityPrintState.x : sliceSettings.x;
		float bias = g_vUnityPrintState.z > 0 ? g_vUnityPrintState.y : sliceSettings.w;
		float slice = (i.flPrintPosition - height) * (1 - 2 * g_flPrintDirection);
		float lower = max( edge, max( slice, bias ) ) - sliceSettings.y;
		float limit = max( 0, edge - lower );
		float reveal = max( 0, max( slice, bias ) - lower );
		clip( limit - reveal );
		float rampU = i.bIsFrontFace ? reveal / max( limit, 0.0001 ) : 1;
		float boost = (g_nUnityAnimatedMaterialChannels & 4096) != 0 ? g_flUnityAnimatedPrintBoost : g_vPrintTint.x;
		float4 ramp = SampleLayer( g_tPrintRamp, float2( rampU, 0.5 ), g_vPrintRampSampler ) * boost;
		albedo = lerp( albedo, ramp.rgb, ramp.a * g_vPrintTint.y );
		emission += ramp.rgb * (1 - g_vPrintTint.y);
	}

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		ApplyUnityDither( i.vPositionSs.xy, g_flFade - g_flFadeBias );
		float2 uv = i.vUnityUv * g_vBaseST.xy + g_vBaseST.zw;
		float4 color = SampleLayer( g_tColor, uv, g_vColorSampler ) * g_vColorTint;
		#if S_CUTOUT
			clip( color.a - 0.5 );
		#endif

		float3 albedo = color.rgb;
		float3 weights = float3( 1, 0, 0 );
		float smoothness = g_flSmoothness;
		if ( g_bSplat )
		{
			float2 layerUv = uv * g_vSplatSettings.x;
			float4 green = SampleLayer( g_tGreen, layerUv, g_vGreenSampler );
			float4 blue = SampleLayer( g_tBlue, layerUv, g_vBlueSampler );
			float3 mask = i.vSplatMask;
			if ( g_bUseSplatTexture )
			{
				float2 splatUv = i.vUnityUv * g_vSplatST.xy + g_vSplatST.zw;
				mask = SampleLayer( g_tSplat, splatUv, g_vSplatSampler ).rgb;
			}
			float3 heights = mask + float3( color.a, green.a + g_vSplatSettings.z, blue.a + g_vSplatSettings.w );
			weights = SplatWeights( heights, g_vSplatSettings.y );
			albedo = color.rgb * weights.x + green.rgb * weights.y + blue.rgb * weights.z;
			smoothness = dot( weights, float3( g_flSmoothness, g_vSplatSmoothness ) );
		}
		float3 emissionTint = AnimatedChannels( float4( g_vEmissionTint, 1 ), g_vUnityAnimatedEmissionTint, 8 ).rgb;
		float emissionPower = (g_nUnityAnimatedMaterialChannels & 8192) != 0 ? g_flUnityAnimatedEmissionPower : g_flEmissionPower;
		float3 emission = SampleLayer( g_tEmission, uv, g_vEmissionSampler ).rgb * emissionTint * emissionPower;
		float3 normal = normalize( i.vNormalWs );
		float3 detail = float3( 0, 0, 1 );
		float4 flatNormal = float4( 0.5, 0.5, 1, 1 );
		float2 normalUv = i.vUnityUv * g_vNormalST.xy + g_vNormalST.zw;
		if ( g_bFlow )
		{
			float4 encoded;
			emission += FlowSurface( uv, encoded );
			if ( g_bHasNormal ) detail = UnpackUnityNormal( encoded );
		}
		else if ( g_bSplat )
		{
			float4 red = g_bHasNormal ? SampleLayer( g_tNormal, normalUv, g_vNormalSampler ) : flatNormal;
			float4 green = g_vSplatNormals.x ? SampleLayer( g_tGreenNormal, normalUv * g_vSplatSettings.x, g_vGreenNormalSampler ) : flatNormal;
			float4 blue = g_vSplatNormals.y ? SampleLayer( g_tBlueNormal, normalUv * g_vSplatSettings.x, g_vBlueNormalSampler ) : flatNormal;
			detail = UnpackUnityNormal( red * weights.x + green * weights.y + blue * weights.z );
		}
		else if ( g_bHasNormal )
		{
			// Packed Unity normals store a hemisphere octahedron in RG.
			float2 rg = SampleLayer( g_tNormal, normalUv, g_vNormalSampler ).rg;
			float2 xy = float2( rg.x + rg.y - 256.0 / 255.0, rg.x - rg.y );
			detail = normalize( float3( xy, 1 - abs( xy.x ) - abs( xy.y ) ) );
		}
		if ( g_bHasNormal || g_bSplat )
		{
			detail = lerp( float3( 0, 0, 1 ), detail, g_flNormalStrength );
			normal = normalize( TransformNormal( detail, normal, i.vTangentUWs, i.vTangentVWs ) );
		}
		if ( IsPrintEnabled() ) ApplyPrintCutoff( i, albedo, emission );
		if ( DepthNormals::WantsDepthNormals() ) return DepthNormals::Output( normal );

		#if S_FRESNEL
			float facing = saturate( dot( normal, normalize( -i.vPositionWithOffsetWs ) ) );
			float mask = SampleLayer( g_tFresnelMask, uv, g_vFresnelMaskSampler ).r;
			float rampU = pow( saturate( mask * facing ), g_flFresnelPower );
			float3 ramp = SampleLayer( g_tFresnelRamp, float2( rampU, 0.5 ), g_vFresnelRampSampler ).rgb;
			float boost = (g_nUnityAnimatedMaterialChannels & 16384) != 0 ? g_flUnityAnimatedFresnelBoost : g_flFresnelBoost;
			albedo *= 1 + ramp * boost;
		#endif

		Material m = Material::Init( i );
		m.Albedo = albedo;
		m.Emission = emission;
		m.Normal = normal;
		m.Roughness = 1 - saturate( smoothness );
		return ShadingModelStandard::Shade( m );
	}
}

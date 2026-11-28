HEADER
{
	Description = "Scrolling cloud masks and color-ramp remapping for Unity effects";
}

FEATURES
{
	#include "common/features.hlsl"
	Feature( F_CULL_MODE, 0..2 ( 0="Off", 1="Front", 2="Back" ), "Face culling" );
	Feature( F_ALWAYS_DRAW, 0..1, "Ignore depth test" );
	Feature( F_CLOUD_VARIANT, 0..3 ( 0="Standard", 1="Intersection", 2="Optimized", 3="Legacy Soft Additive" ), "Effect shader variant" );
}

MODES
{
	Forward();
}

COMMON
{
	#include "common/shared.hlsl"
	#define CUSTOM_MATERIAL_INPUTS
	StaticCombo( S_ADDITIVE_BLEND, F_ADDITIVE_BLEND, Sys( ALL ) );
	StaticCombo( S_CULL_MODE, F_CULL_MODE, Sys( ALL ) );
	StaticCombo( S_ALWAYS_DRAW, F_ALWAYS_DRAW, Sys( ALL ) );
	StaticCombo( S_CLOUD_VARIANT, F_CLOUD_VARIANT, Sys( ALL ) );
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
	float4 vColor : COLOR0 < Semantic( Color ); >;
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
	float4 vColor : COLOR0;
	float4 vUv : TEXCOORD12;
};

VS
{
	#include "common/vertex.hlsl"
	float g_flVertexOffset < Default( 0 ); >;
	float4 g_vParticleTint < Attribute( "UnityParticleTint" ); Default4( 1, 1, 1, 1 ); >;

	PixelInput MainVs( VertexInput v )
	{
		float3 normal;
		float4 tangent;
		VS_DecodeObjectSpaceNormalAndTangent( v, normal, tangent );
		v.vPositionOs += normalize( normal ) * g_flVertexOffset;
		PixelInput i = ProcessVertex( v );
		i.vColor = v.vColor * g_vParticleTint;
		i.vUv = float4( v.vTexCoord, v.vTexCoord2 );
		return FinalizeVertex( i );
	}
}

PS
{
	#include "common/pixel.hlsl"
	#include "common/classes/Depth.hlsl"

	BoolAttribute( translucent, true );
	RenderState( BlendEnable, true );
	RenderState( DepthWriteEnable, false );
	#if S_CLOUD_VARIANT == 2 || S_CLOUD_VARIANT == 3
		RenderState( ColorWriteEnable0, RGB );
	#endif
	#if S_ALWAYS_DRAW
		RenderState( DepthEnable, false );
	#endif
	#if S_CLOUD_VARIANT == 3
		RenderState( SrcBlend, ONE );
		RenderState( DstBlend, INV_SRC_COLOR );
	#elif S_ADDITIVE_BLEND
		RenderState( SrcBlend, ONE );
		RenderState( DstBlend, ONE );
		RenderState( SrcBlendAlpha, ONE );
		RenderState( DstBlendAlpha, ONE );
	#else
		RenderState( SrcBlend, SRC_ALPHA );
		RenderState( DstBlend, INV_SRC_ALPHA );
		RenderState( SrcBlendAlpha, SRC_ALPHA );
		RenderState( DstBlendAlpha, INV_SRC_ALPHA );
	#endif
	#if S_CULL_MODE == 0
		RenderState( CullMode, NONE );
	#elif S_CULL_MODE == 1
		RenderState( CullMode, FRONT );
	#else
		RenderState( CullMode, BACK );
	#endif

	CreateInputTexture2D( Main, Srgb, 8, "None", "_color", "", Default4( 0.5, 0.5, 0.5, 1 ) );
	CreateInputTexture2D( CloudFirst, Linear, 8, "None", "", "", Default4( 0.5, 0.5, 0.5, 1 ) );
	CreateInputTexture2D( CloudSecond, Linear, 8, "None", "", "", Default4( 0.5, 0.5, 0.5, 1 ) );
	CreateInputTexture2D( Remap, Srgb, 8, "None", "", "", Default4( 0.5, 0.5, 0.5, 1 ) );
	Texture2D g_tColor < Channel( RGBA, Box( Main ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	Texture2D g_tCloudFirst < Channel( RGBA, Box( CloudFirst ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	Texture2D g_tCloudSecond < Channel( RGBA, Box( CloudSecond ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	Texture2D g_tRemap < Channel( RGBA, Box( Remap ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;

	float4 g_vMainST < Default4( 1, 1, 0, 0 ); >;
	float4 g_vFirstST < Default4( 1, 1, 0, 0 ); >;
	float4 g_vSecondST < Default4( 1, 1, 0, 0 ); >;
	float4 g_vScroll < Default4( 0, 0, 0, 0 ); >;
	float4 g_vTint < Default4( 1, 1, 1, 1 ); >;
	float4 g_vSamplerIndices < Default4( 0, 0, 0, 0 ); >;
	float4 g_vMirrorV < Default4( 0, 0, 0, 0 ); >;
	float g_flBoost < Default( 1 ); >;
	float g_flAlphaBoost < Default( 1 ); >;
	float g_flAlphaBias < Default( 0 ); >;
	float g_flFade < Default( 1 ); >;
	float g_flExternalAlpha < Default( 1 ); >;
	uint g_nUnityAnimatedMaterialChannels < Attribute( "UnityAnimatedMaterialChannels" ); Default( 0 ); >;
	float g_flUnityAnimatedExternalAlpha < Attribute( "UnityAnimatedExternalAlpha" ); Default( 0 ); >;
	float g_flDistortion < Default( 0.1 ); >;
	float g_flInverseFade < Default( 0 ); >;
	float g_flCloseFade < Default( 0 ); >;
	float g_flDepthOffset < Default( 0 ); >;
	float g_flFresnelPower < Default( 0 ); >;
	float g_bClouds < Default( 0 ); >;
	float g_bDistort < Default( 0 ); >;
	float g_bVertexColor < Default( 0 ); >;
	float g_bVertexAlpha < Default( 0 ); >;
	float g_bDisableRemap < Default( 0 ); >;
	float g_bSkyOnly < Default( 0 ); >;
	float g_bUseUv1 < Default( 0 ); >;
	#if S_CLOUD_VARIANT == 1
		// Soft depth power, rim power, rim strength and intersection strength.
		float4 g_vIntersectionStyle < Default4( 1, 1, 1, 0 ); >;
		float g_bTriplanar < Default( 0 ); >;
	#elif S_CLOUD_VARIANT == 2
		float4 g_vDistortedMainST < Default4( 1, 1, 0, 0 ); >;
		float4 g_vTexCoordST < Default4( 1, 1, 0, 0 ); >;
		float g_flRemapFactor < Default( 0 ); >;
	#endif

	float ExternalAlpha()
	{
		return (g_nUnityAnimatedMaterialChannels & 32768) != 0 ? g_flUnityAnimatedExternalAlpha : g_flExternalAlpha;
	}

	float2 FlipV( float2 uv )
	{
		return float2( uv.x, 1 - uv.y );
	}

	float4 SampleUnityTexture( Texture2D texture, float2 uv, float samplerIndex, float mirrorV )
	{
		// Mirror-once is centered at Unity V=0. Flipping first would move the mirror to the wrong edge.
		if ( mirrorV ) uv.y = abs( uv.y );
		return texture.Sample( Bindless::GetSampler( asDynamicUniform( (int)samplerIndex ) ), FlipV( uv ) );
	}

	#if S_CLOUD_VARIANT == 1
		float SampleIntersectionCloud( Texture2D texture, float2 uv, float3 position, float3 weights,
			float4 transform, float2 scroll, float samplerIndex, float mirrorV )
		{
			if ( !g_bTriplanar )
				return SampleUnityTexture( texture, uv * transform.xy + transform.zw + scroll, samplerIndex, mirrorV ).a;
			float3 masks = float3(
				SampleUnityTexture( texture, (position.zy + scroll) * transform.xy, samplerIndex, mirrorV ).a,
				SampleUnityTexture( texture, (position.xz + scroll) * transform.xy, samplerIndex, mirrorV ).a,
				SampleUnityTexture( texture, (position.xy + scroll) * transform.xy, samplerIndex, mirrorV ).a );
			return dot( weights, masks );
		}

		float4 ShadeIntersection( PixelInput i, float2 uv, float mask )
		{
			float3 normal = normalize( i.vNormalWs );
			float3 weights = abs( float3( -normal.y, normal.z, normal.x ) );
			weights /= max( weights.x + weights.y + weights.z, 0.0001 );
			float3 world = i.vPositionWithOffsetWs + g_vCameraPositionWs;
			float3 position = float3( -world.y, world.z, world.x ) / 39.3700787;
			float4 scroll = g_vScroll * (g_flTime / 20);
			float first = SampleIntersectionCloud( g_tCloudFirst, uv, position, weights, g_vFirstST, scroll.xy, g_vSamplerIndices.y, g_vMirrorV.y );
			float second = SampleIntersectionCloud( g_tCloudSecond, uv, position, weights, g_vSecondST, scroll.zw, g_vSamplerIndices.z, g_vMirrorV.z );
			mask *= 4 * first * second * g_flAlphaBoost * g_flFade * ExternalAlpha();

			float softDepth = 1;
			if ( g_flInverseFade > 0 )
			{
				float viewDepth = dot( i.vPositionWithOffsetWs, g_vCameraDirWs );
				float2 screen = i.vPositionSs.xy;
				float depthGap = Depth::Linearize( Depth::GetNormalized( screen ), screen ) - viewDepth;
				softDepth = pow( saturate( depthGap * g_flInverseFade ), g_vIntersectionStyle.x );
			}
			float facing = abs( dot( normalize( -i.vPositionWithOffsetWs ), normal ) );
			float rim = pow( saturate( 1 - facing ), g_vIntersectionStyle.y ) * g_vIntersectionStyle.z;
			float ramp = saturate( mask * (rim + 1 - softDepth) * g_vIntersectionStyle.w );
			float4 color = SampleUnityTexture( g_tRemap, float2( ramp, 0.5 ), g_vSamplerIndices.w, g_vMirrorV.w ) * g_vTint;
			return float4( color.rgb * g_flBoost, color.a * (g_bVertexAlpha ? i.vColor.r : 1) );
		}
	#elif S_CLOUD_VARIANT == 2
		float4 ShadeOptimized( PixelInput i, float2 uv, float mainAlpha )
		{
			float2 offset = 1;
			if ( g_bClouds )
			{
				float2 cloudUv = uv * g_vFirstST.xy + g_vScroll.xy * (g_flTime * 0.1);
				float cloudAlpha = SampleUnityTexture( g_tCloudFirst, cloudUv, g_vSamplerIndices.y, g_vMirrorV.y ).a;
				offset = (cloudAlpha - 0.5) * g_flDistortion;
			}
			float2 warped = uv * g_vDistortedMainST.xy + g_vDistortedMainST.zw + offset;
			float warpedAlpha = SampleUnityTexture( g_tColor, warped, g_vSamplerIndices.x, g_vMirrorV.x ).a;
			float opacity = saturate( saturate( mainAlpha * g_flAlphaBias ) * warpedAlpha * i.vColor.r * i.vColor.a * g_flAlphaBoost );
			float3 color = g_vTint.rgb;
			if ( !g_bDisableRemap )
			{
				float ramp = warpedAlpha * g_flRemapFactor;
				color += SampleUnityTexture( g_tRemap, float2( ramp, ramp ), g_vSamplerIndices.w, g_vMirrorV.w ).rgb;
			}
			if ( g_bVertexColor ) color *= i.vColor.rgb;
			return float4( color * g_flBoost, opacity );
		}
	#endif

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		float2 screen = i.vPositionSs.xy;
		if ( g_bSkyOnly ) clip( -Depth::GetNormalized( screen ) );

		// Keep the authored offsets, scroll and distortion in Unity UV space.
		float2 uv = FlipV( g_bUseUv1 ? i.vUv.zw : i.vUv.xy );
		#if S_CLOUD_VARIANT == 2
			uv = uv * g_vTexCoordST.xy + g_vTexCoordST.zw;
		#endif
		float2 mainUv = uv * g_vMainST.xy + g_vMainST.zw;
		float4 baseSample = SampleUnityTexture( g_tColor, mainUv, g_vSamplerIndices.x, g_vMirrorV.x );
		float mask = baseSample.a * i.vColor.a * g_vTint.a;
		#if S_CLOUD_VARIANT == 1
			return ShadeIntersection( i, uv, mask );
		#elif S_CLOUD_VARIANT == 2
			return ShadeOptimized( i, uv, baseSample.a );
		#elif S_CLOUD_VARIANT == 3
			float viewDepth = dot( i.vPositionWithOffsetWs, g_vCameraDirWs );
			float fade = saturate( (Depth::GetLinear( screen ) - viewDepth) * g_flInverseFade );
			float4 softColor = baseSample * i.vColor;
			float alpha = softColor.a * fade;
			return float4( softColor.rgb * alpha, alpha );
		#endif
		if ( g_bClouds )
		{
			float phase = g_flTime / 20;
			if ( !g_bVertexColor && !g_bVertexAlpha ) phase += dot( i.vColor.rgb, float3( 1, 1, 1 ) );
			float2 firstUv = uv * g_vFirstST.xy + g_vFirstST.zw + phase * g_vScroll.xy;
			float2 secondUv = uv * g_vSecondST.xy + g_vSecondST.zw + phase * g_vScroll.zw;
			float first = SampleUnityTexture( g_tCloudFirst, firstUv, g_vSamplerIndices.y, g_vMirrorV.y ).a;
			float second = SampleUnityTexture( g_tCloudSecond, secondUv, g_vSamplerIndices.z, g_vMirrorV.z ).a;
			float cloud = first * second;
			if ( g_bDistort )
			{
				float2 warped = mainUv + float2( 0, (2 * cloud - 1) * g_flDistortion );
				mask *= SampleUnityTexture( g_tColor, warped, g_vSamplerIndices.x, g_vMirrorV.x ).a * i.vColor.a * g_vTint.a;
			}
			else
			{
				mask *= 4 * cloud;
			}
		}
		mask = mask * g_flAlphaBoost + g_flAlphaBias;
		if ( g_bDisableRemap ) return float4( baseSample.rgb * i.vColor.rgb * g_vTint.rgb * g_flBoost, mask );

		float ramp = mask * g_flFade * ExternalAlpha();
		if ( g_bVertexAlpha ) ramp *= i.vColor.r;
		if ( g_flFresnelPower != 0 )
		{
			float facing = dot( normalize( -i.vPositionWithOffsetWs ), normalize( i.vNormalWs ) );
			float fresnel = g_flFresnelPower > 0 ? 1 - facing : facing;
			ramp *= pow( saturate( fresnel ), abs( g_flFresnelPower ) );
		}
		if ( g_flInverseFade > 0 )
		{
			float viewDepth = dot( i.vPositionWithOffsetWs, g_vCameraDirWs ) + g_flDepthOffset;
			ramp *= saturate( (Depth::GetLinear( screen ) - viewDepth) * g_flInverseFade );
			if ( g_flCloseFade > 0 ) ramp *= saturate( (viewDepth / 39.3700787 - 0.5) * g_flCloseFade );
		}
		float4 color = SampleUnityTexture( g_tRemap, float2( ramp, 0.5 ), g_vSamplerIndices.w, g_vMirrorV.w ) * g_vTint;
		if ( g_bVertexColor ) color.rgb *= i.vColor.rgb;
		return float4( color.rgb * g_flBoost, color.a );
	}
}

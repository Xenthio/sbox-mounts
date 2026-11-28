HEADER
{
	Description = "Unity particle refraction with authored normal, mask and depth fade";
}

MODES
{
	Forward();
}

COMMON
{
	#include "common/shared.hlsl"
	#define CUSTOM_MATERIAL_INPUTS
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
	float4 vColor : COLOR0 < Semantic( Color ); >;
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
	float vParticleAlpha : TEXCOORD12;
	float2 vParticleUv : TEXCOORD13;
	float vClipW : TEXCOORD14;
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput v )
	{
		PixelInput i = ProcessVertex( v );
		i.vParticleAlpha = v.vColor.a;
		i.vParticleUv = v.vTexCoord;
		i.vClipW = i.vPositionPs.w;
		return FinalizeVertex( i );
	}
}

PS
{
	#include "common/pixel.hlsl"
	#include "common/classes/Depth.hlsl"

	BoolAttribute( translucent, true );
	RenderState( BlendEnable, true );
	RenderState( SrcBlend, SRC_ALPHA );
	RenderState( DstBlend, INV_SRC_ALPHA );
	RenderState( SrcBlendAlpha, ONE );
	RenderState( DstBlendAlpha, INV_SRC_ALPHA );
	RenderState( DepthWriteEnable, false );
	RenderState( CullMode, NONE );

	CreateInputTexture2D( Normal, Linear, 8, "None", "", "", Default4( 0.5, 0.5, 1, 1 ) );
	CreateInputTexture2D( Mask, Linear, 8, "None", "", "", Default4( 1, 1, 1, 1 ) );
	Texture2D g_tNormal < Channel( RGBA, Box( Normal ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	Texture2D g_tMask < Channel( RGBA, Box( Mask ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	Texture2D g_tScene < Attribute( "UnityDistortionScene" ); SrgbRead( false ); >;

	float4 g_vNormalST < Default4( 1, 1, 0, 0 ); >;
	float4 g_vMaskST < Default4( 1, 1, 0, 0 ); >;
	float4 g_vSamplers < Default4( 0, 0, 0, 0 ); >;
	float4 g_vCameraFade < Default4( 0, 5, 250000, 200000 ); >;
	float g_flMagnitude < Default( 0.05 ); >;
	float g_flInverseFade < Default( 0.1 ); >;
	float g_flDistanceModulation < Default( 0 ); >;
	float g_flInchesPerMeter < Default( 39.37008 ); >;

	float4 SampleUnity( Texture2D texture, float2 uv, float4 transform, float samplerIndex, float mirrorV )
	{
		float2 sourceUv = (float2( uv.x, 1 - uv.y ) + transform.zw) * transform.xy;
		if ( mirrorV > 0.5 ) sourceUv.y = abs( sourceUv.y );
		sourceUv.y = 1 - sourceUv.y;
		return texture.Sample( Bindless::GetSampler( asDynamicUniform( (int)samplerIndex ) ), sourceUv );
	}

	float Fade( float depth, float zeroAt, float oneAt )
	{
		return zeroAt == oneAt ? step( zeroAt, depth ) : saturate( (depth - zeroAt) / (oneAt - zeroAt) );
	}

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		// Unity offsets the grab coordinates before dividing by clip W, in source meters.
		float clipW = max( i.vClipW / g_flInchesPerMeter, 0.00001 );
		float opacity = i.vParticleAlpha * SampleUnity( g_tMask, i.vParticleUv, g_vMaskST, g_vSamplers.y, g_vSamplers.w ).a;
		opacity *= min( Fade( clipW, g_vCameraFade.x, g_vCameraFade.y ), Fade( clipW, g_vCameraFade.z, g_vCameraFade.w ) );

		if ( g_flInverseFade > 0 || g_flDistanceModulation != 0 )
		{
			float sceneDepth = Depth::GetLinear( i.vPositionSs.xy );
			if ( g_flInverseFade > 0 )
				opacity *= saturate( (sceneDepth / g_flInchesPerMeter - clipW) * g_flInverseFade );
			if ( g_flDistanceModulation != 0 )
				opacity *= lerp( 1, saturate( sceneDepth / Depth::Linearize( 0 ) ), g_flDistanceModulation );
		}
		if ( opacity < 0.01 ) return 0;

		float4 normal = SampleUnity( g_tNormal, i.vParticleUv, g_vNormalST, g_vSamplers.x, g_vSamplers.z );
		float2 offset = float2( normal.r * normal.a, normal.g ) * 2 - 1;
		if ( dot( offset, offset ) > 1 - 0.99 * 0.99 ) return 0;

		float2 halfTexel = g_vInvViewportSize * 0.5;
		float2 screenUv = CalculateViewportUv( i.vPositionSs.xy ) + offset * (g_flMagnitude * opacity / clipW);
		float2 copyUv = clamp( screenUv, halfTexel, 1 - halfTexel );
		return float4( g_tScene.SampleLevel( g_sBilinearClamp, copyUv, 0 ).rgb, 1 );
	}
}

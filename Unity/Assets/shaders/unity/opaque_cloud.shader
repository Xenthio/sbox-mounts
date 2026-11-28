HEADER
{
	Description = "Depth-writing cloud masks and cutout effects for Unity materials";
}

FEATURES
{
	#include "common/features.hlsl"
	Feature( F_OPTIMIZED, 0..1, "Optimized cloud material" );
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
	float4 vColor : COLOR0;
	float2 vCloudUv : TEXCOORD12;
};

VS
{
	#include "common/vertex.hlsl"
	float4 g_vParticleTint < Attribute( "UnityParticleTint" ); Default4( 1, 1, 1, 1 ); >;

	PixelInput MainVs( VertexInput v )
	{
		PixelInput i = ProcessVertex( v );
		i.vColor = v.vColor * g_vParticleTint;
		i.vCloudUv = float2( v.vTexCoord.x, 1 - v.vTexCoord.y );
		return FinalizeVertex( i );
	}
}

PS
{
	#include "common/pixel.hlsl"

	RenderState( BlendEnable, false );
	RenderState( DepthWriteEnable, true );
	#if S_CULL_MODE == 0
		RenderState( CullMode, NONE );
	#elif S_CULL_MODE == 1
		RenderState( CullMode, FRONT );
	#else
		RenderState( CullMode, BACK );
	#endif

	CreateInputTexture2D( Main, Linear, 8, "", "", "", Default4( 0.5, 0.5, 0.5, 1 ) );
	CreateInputTexture2D( First, Linear, 8, "", "", "", Default4( 1, 1, 1, 1 ) );
	CreateInputTexture2D( Second, Linear, 8, "", "", "", Default4( 1, 1, 1, 1 ) );
	CreateInputTexture2D( Ramp, Srgb, 8, "", "", "", Default4( 0.5, 0.5, 0.5, 1 ) );
	CreateInputTexture2D( NormalMap, Linear, 8, "", "", "", Default4( 0.5, 0.5, 1, 1 ) );
	Texture2D g_tColor < Channel( RGBA, Box( Main ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	Texture2D g_tCloudFirst < Channel( RGBA, Box( First ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	Texture2D g_tCloudSecond < Channel( RGBA, Box( Second ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	Texture2D g_tRemap < Channel( RGBA, Box( Ramp ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	Texture2D g_tNormal < Channel( RGBA, Box( NormalMap ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;

	float4 g_vMainST < Default4( 1, 1, 0, 0 ); >;
	float4 g_vFirstST < Default4( 1, 1, 0, 0 ); >;
	float4 g_vSecondST < Default4( 1, 1, 0, 0 ); >;
	float4 g_vDistortedMainST < Default4( 1, 1, 0, 0 ); >;
	float4 g_vScroll < Default4( 0, 0, 0, 0 ); >;
	float4 g_vTint < Default4( 1, 1, 1, 1 ); >;
	float4 g_vEmission < Default4( 1, 1, 1, 1 ); >;
	float4 g_vFresnelTint < Default4( 1, 1, 1, 1 ); >;
	float4 g_vSamplerIndices < Default4( 0, 0, 0, 0 ); >;
	float4 g_vMirrorV < Default4( 0, 0, 0, 0 ); >;
	float4 g_vNormalSampler < Default4( 0, 0, 0, 0 ); >;
	float g_flCutoff < Default( 0.5 ); >;
	float g_flAlphaBoost < Default( 1 ); >;
	float g_flAlphaBias < Default( 0.2 ); >;
	float g_flExternalAlpha < Default( 1 ); >;
	uint g_nUnityAnimatedMaterialChannels < Attribute( "UnityAnimatedMaterialChannels" ); Default( 0 ); >;
	float g_flUnityAnimatedExternalAlpha < Attribute( "UnityAnimatedExternalAlpha" ); Default( 0 ); >;
	float g_flBoost < Default( 1 ); >;
	float g_flVertexInfluence < Default( 1 ); >;
	float g_flNormalStrength < Default( 1 ); >;
	float g_flDistortion < Default( 0 ); >;
	float g_flRemapFactor < Default( 0 ); >;
	float g_flFresnelPower < Default( 0 ); >;
	float g_bClouds < Default( 0 ); >;
	float g_bCloudNormal < Default( 0 ); >;
	float g_bEmissionFromAlbedo < Default( 0 ); >;
	float g_bVertexAlpha < Default( 0 ); >;
	float g_bVertexColor < Default( 0 ); >;
	float g_bDisableRemap < Default( 0 ); >;
	float g_bHasNormal < Default( 0 ); >;
	float g_bHasRemap < Default( 0 ); >;

	float4 SampleCloudTexture( Texture2D texture, float2 unityUv, float samplerIndex, float mirrorV )
	{
		// Import flips the stored texture rows. Mirror-once still needs to reflect around Unity's V origin.
		float v = mirrorV ? saturate( abs( unityUv.y ) ) : unityUv.y;
		return texture.Sample( Bindless::GetSampler( asDynamicUniform( (int)samplerIndex ) ), float2( unityUv.x, 1 - v ) );
	}

	float2 LayerUv( float2 uv, float4 st )
	{
		return uv * st.xy + st.zw;
	}

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		float2 uv = i.vCloudUv;
		float2 mainUv = LayerUv( uv, g_vMainST );
		float main = SampleCloudTexture( g_tColor, mainUv, g_vSamplerIndices.x, g_vMirrorV.x ).a;
		float3 normal = normalize( i.vNormalWs );
		#if S_OPTIMIZED
			float2 distortedUv = LayerUv( uv, g_vDistortedMainST );
			if ( g_bClouds )
			{
				float2 firstUv = LayerUv( uv, g_vFirstST ) + g_vScroll.xy * (g_flTime / 10);
				float cloud = SampleCloudTexture( g_tCloudFirst, firstUv, g_vSamplerIndices.y, g_vMirrorV.y ).a;
				distortedUv += (cloud - 0.5) * g_flDistortion;
			}
			else distortedUv += 1;
			float warped = SampleCloudTexture( g_tColor, distortedUv, g_vSamplerIndices.x, g_vMirrorV.x ).a;
			float mask = saturate( saturate( main * g_flAlphaBias ) * warped * i.vColor.r * i.vColor.a * g_flAlphaBoost );
			clip( mask - g_flCutoff );
			if ( DepthNormals::WantsDepthNormals() ) return DepthNormals::Output( normal );
			float3 color = g_vTint.rgb;
			if ( !g_bDisableRemap )
				color += SampleCloudTexture( g_tRemap, warped * g_flRemapFactor, g_vSamplerIndices.w, g_vMirrorV.w ).rgb;
			if ( g_bVertexColor ) color *= i.vColor.rgb;
			return float4( color * g_flBoost, 1 );
		#else
			float2 firstUv = LayerUv( uv, g_vFirstST ) + g_vScroll.xy * (g_flTime / 20);
			float2 secondUv = LayerUv( uv, g_vSecondST ) + g_vScroll.zw * (g_flTime / 20);
			float cloud = SampleCloudTexture( g_tCloudFirst, firstUv, g_vSamplerIndices.y, g_vMirrorV.y ).a
				* SampleCloudTexture( g_tCloudSecond, secondUv, g_vSamplerIndices.z, g_vMirrorV.z ).a;
			float4 tint = lerp( 1, i.vColor, g_flVertexInfluence );
			float externalAlpha = (g_nUnityAnimatedMaterialChannels & 32768) != 0 ? g_flUnityAnimatedExternalAlpha : g_flExternalAlpha;
			float mask = 4 * main * cloud * tint.a * tint.a * externalAlpha * g_vTint.a * g_flAlphaBoost;
			if ( g_bVertexAlpha ) mask *= i.vColor.r;
			clip( mask - g_flCutoff );

			if ( g_bHasNormal )
			{
				float3 tangentNormal = DecodeNormal( SampleCloudTexture( g_tNormal, g_bCloudNormal ? firstUv : mainUv,
					g_vNormalSampler.x, g_vNormalSampler.y ).rgb );
				tangentNormal = normalize( lerp( float3( 0, 0, 1 ), tangentNormal, g_flNormalStrength ) );
				normal = TransformNormal( tangentNormal, normal, i.vTangentUWs, i.vTangentVWs );
			}
			Material m = Material::Init( i );
			float3 ramp = g_bHasRemap
				? SampleCloudTexture( g_tRemap, float2( mask, 0.5 ), g_vSamplerIndices.w, g_vMirrorV.w ).rgb
				: SrgbGammaToLinear( float3( 0.5, 0.5, 0.5 ) );
			m.Albedo = ramp * tint.rgb * g_vTint.rgb;
			m.Normal = normal;
			m.Roughness = 1;
			m.Opacity = 1;
			m.Emission = g_vEmission.rgb * (g_bEmissionFromAlbedo ? m.Albedo : 1);
			if ( g_flFresnelPower != 0 )
			{
				float facing = saturate( dot( normal, normalize( -i.vPositionWithOffsetWs ) ) );
				float rim = g_flFresnelPower > 0 ? 1 - facing : facing;
				m.Emission *= g_vFresnelTint.rgb * g_vFresnelTint.a * pow( rim, abs( g_flFresnelPower ) );
			}
			return ShadingModelStandard::Shade( m );
		#endif
	}
}

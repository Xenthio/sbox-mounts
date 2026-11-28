HEADER
{
	Description = "Unity wavy cloth surfaces";
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
	float flVertexRed : TEXCOORD13;
};

VS
{
	#include "common/vertex.hlsl"

	float4 g_vScroll < Default4( 0, 0, 0, 0 ); >;
	float3 g_vWind < Default3( 0, 0, 0 ); >;
	float g_flVertexOffset < Default( 0 ); >;
	float g_bVertexRed < Default( 0 ); >;

	float ValueNoise( float3 position )
	{
		float cell = dot( floor( position ), float3( 1, 57, 113 ) );
		float3 f = frac( position );
		f = f * f * (3 - 2 * f);
		float4 lower = frac( sin( cell + float4( 0, 1, 57, 58 ) ) * 43758.546875 );
		float4 upper = frac( sin( cell + float4( 113, 114, 170, 171 ) ) * 43758.546875 );
		float4 corners = lerp( lower, upper, f.z );
		return lerp( lerp( corners.x, corners.y, f.x ), lerp( corners.z, corners.w, f.x ), f.y );
	}

	PixelInput MainVs( VertexInput v )
	{
		float2 uv = float2( v.vTexCoord.x, 1 - v.vTexCoord.y );
		float bend = g_bVertexRed ? v.vColor.r : uv.x;
		float3x4 transform = GetTransformMatrix( v.nInstanceTransformID );
		float3 origin = mul( transform, float4( 0, 0, 0, 1 ) );
		// Undo the mesh axis and unit conversion before evaluating Unity's object-seeded noise.
		origin = float3( -origin.y, origin.z, origin.x ) * 0.0254;
		float2 phase = uv + (g_flTime / 20) * g_vScroll.xy;
		float3 offset = float3(
			ValueNoise( float3( (phase + origin.x) * g_vScroll.zw, origin.x ) ),
			ValueNoise( float3( (phase + origin.z) * g_vScroll.zw, origin.z ) ),
			ValueNoise( float3( (phase + origin.y) * g_vScroll.zw, origin.y ) ) ) * (g_flVertexOffset * bend);
		float3 localNormal;
		float4 localTangent;
		VS_DecodeObjectSpaceNormalAndTangent( v, localNormal, localTangent );
		float3 unityNormal = float3( -localNormal.y, localNormal.z, localNormal.x );
		offset -= cross( unityNormal, offset );
		v.vPositionOs.xyz += float3( offset.z, -offset.x, offset.y );

		PixelInput i = ProcessVertex( v );
		i.vPositionWs += g_vWind * (bend * bend);
		i.vPositionPs = Position3WsToPs( i.vPositionWs );
		i.vUnityUv = uv;
		i.flVertexRed = v.vColor.r;
		return FinalizeVertex( i );
	}
}

PS
{
	#include "common/pixel.hlsl"
	BoolAttribute( VertexNeedsPropOrigin, true );

	CreateInputTexture2D( Color, Srgb, 8, "", "", "", Default4( 1, 1, 1, 1 ) );
	CreateInputTexture2D( Normal, Linear, 8, "", "", "", Default4( 0.5, 0.5, 1, 1 ) );
	Texture2D g_tColor < Channel( RGBA, Box( Color ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	Texture2D g_tNormal < Channel( RGBA, Box( Normal ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	float2 g_vColorSampler < Default2( 0, 0 ); >;
	float2 g_vNormalSampler < Default2( 0, 0 ); >;
	float4 g_vColorST < Default4( 1, 1, 0, 0 ); >;
	float4 g_vNormalST < Default4( 1, 1, 0, 0 ); >;
	float4 g_vTint < Default4( 0.5, 0.5, 0.5, 1 ); >;
	float g_flCutoff < Default( 0.5 ); >;
	float g_flNormalStrength < Default( 0 ); >;
	float g_flSmoothness < Default( 0 ); >;

	float4 SampleLayer( Texture2D texture, float2 uv, float2 binding )
	{
		if ( binding.y ) uv.y = abs( uv.y );
		return texture.Sample( Bindless::GetSampler( asDynamicUniform( (int)binding.x ) ), float2( uv.x, 1 - uv.y ) );
	}

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		float2 uv = i.vUnityUv;
		float4 color = SampleLayer( g_tColor, uv * g_vColorST.xy + g_vColorST.zw, g_vColorSampler ) * g_vTint;
		clip( color.a - g_flCutoff );

		float3 normal = normalize( i.vNormalWs );
		if ( g_flNormalStrength != 0 )
		{
			float2 normalUv = uv * g_vNormalST.xy + g_vNormalST.zw + float2( 0, g_flTime / 20 );
			float4 encoded = SampleLayer( g_tNormal, normalUv, g_vNormalSampler );
			float2 xy = float2( encoded.r * encoded.a, encoded.g ) * 2 - 1;
			float3 detail = normalize( float3( xy.x, -xy.y, sqrt( saturate( 1 - dot( xy, xy ) ) ) ) );
			detail = lerp( float3( 0, 0, 1 ), detail, i.flVertexRed * g_flNormalStrength );
			normal = normalize( TransformNormal( detail, normal, i.vTangentUWs, i.vTangentVWs ) );
		}
		if ( DepthNormals::WantsDepthNormals() ) return DepthNormals::Output( normal );

		Material m = Material::Init( i );
		m.Albedo = color.rgb;
		m.Normal = normal;
		m.Roughness = 1 - saturate( g_flSmoothness );
		return ShadingModelStandard::Shade( m );
	}
}

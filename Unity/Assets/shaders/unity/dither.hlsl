#ifndef UNITY_DITHER_HLSL
#define UNITY_DITHER_HLSL

void ApplyUnityDither( float2 screenPosition, float coverage )
{
	if ( coverage >= 1 ) return;
	static const uint Bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
	uint2 cell = (uint2)screenPosition & 3;
	clip( coverage - Bayer[cell.y * 4 + cell.x] / 16.0 );
}

#endif

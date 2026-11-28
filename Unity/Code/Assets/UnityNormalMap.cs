using System.Buffers.Binary;

namespace UnityMount;

/// <summary>Converts Unity's tangent normals to the packed normal input of s&box's Complex shader.</summary>
static class UnityNormalMap
{
	public static void PackMip( ReadOnlySpan<byte> source, Span<byte> target, int width, int height, int format )
	{
		if ( format == UnityTexture.Rgba32 )
		{
			for ( var y = 0; y < height; y++ )
			{
				for ( var x = 0; x < width; x++ )
				{
					var pixel = source.Slice( ((height - 1 - y) * width + x) * 4, 4 );
					// Unity's RG-or-AG unpack uses red multiplied by alpha for X.
					WriteNormal( target, (y * width + x) * 4, pixel[0] * (pixel[3] / 255f), pixel[1] );
				}
			}
			return;
		}

		var blocksWide = (width + 3) / 4;
		Span<byte> alpha = stackalloc byte[8];
		Span<byte> red = stackalloc byte[4];
		Span<byte> green = stackalloc byte[4];
		for ( var blockY = 0; blockY < height; blockY += 4 )
		{
			for ( var blockX = 0; blockX < width; blockX += 4 )
			{
				var offset = (blockY / 4 * blocksWide + blockX / 4) * 16;
				var block = source.Slice( offset, 16 );
				UnityBlockTexture.ReadAlpha( block[0], block[1], alpha );
				var alphaIndices = BinaryPrimitives.ReadUInt64LittleEndian( block ) >> 16;
				var colorIndices = BinaryPrimitives.ReadUInt32LittleEndian( block[12..] );
				var first = BinaryPrimitives.ReadUInt16LittleEndian( block[8..] );
				var second = BinaryPrimitives.ReadUInt16LittleEndian( block[10..] );
				var r0 = first >> 11;
				var r1 = second >> 11;
				var g0 = (first >> 5) & 63;
				var g1 = (second >> 5) & 63;
				ReadColor( (r0 << 3) | (r0 >> 2), (r1 << 3) | (r1 >> 2), red );
				ReadColor( (g0 << 2) | (g0 >> 4), (g1 << 2) | (g1 >> 4), green );

				for ( var row = 0; row < Math.Min( 4, height - blockY ); row++ )
				{
					for ( var column = 0; column < Math.Min( 4, width - blockX ); column++ )
					{
						var pixel = row * 4 + column;
						var color = (int)(colorIndices >> (pixel * 2)) & 3;
						var x = alpha[(int)(alphaIndices >> (pixel * 3)) & 7] * (red[color] / 255f);
						var y = green[color];
						WriteNormal( target, ((height - 1 - blockY - row) * width + blockX + column) * 4, x, y );
					}
				}
			}
		}
	}

	static void ReadColor( int first, int second, Span<byte> values )
	{
		values[0] = (byte)first;
		values[1] = (byte)second;
		values[2] = (byte)((2 * first + second) / 3);
		values[3] = (byte)((first + 2 * second) / 3);
	}

	static void WriteNormal( Span<byte> target, int offset, float red, float green )
	{
		var x = red * (2f / 255) - 1;
		var y = green * (2f / 255) - 1;
		var z = MathF.Sqrt( MathF.Max( 0, 1 - x * x - y * y ) );
		var scale = 127.5f / (MathF.Abs( x ) + MathF.Abs( y ) + z);
		// Inverse of DecodeHemiOctahedronNormal. Source's shader reverses tangent Y
		// to account for the V flip already applied when importing Unity mesh UVs.
		target[offset] = Quantize( 128 + (x + y) * scale );
		target[offset + 1] = Quantize( 128 + (x - y) * scale );
		target[offset + 2] = 128; // Match Complex's default roughness when no roughness map is available.
		target[offset + 3] = 255;
	}

	static byte Quantize( float value ) => (byte)Math.Clamp( MathF.Round( value ), 0, 255 );
}

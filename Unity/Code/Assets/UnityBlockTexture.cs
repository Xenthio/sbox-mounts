using System.Buffers.Binary;

namespace UnityMount;

static class UnityBlockTexture
{
	public struct BitReader( ReadOnlySpan<byte> block )
	{
		ulong _lower = BinaryPrimitives.ReadUInt64LittleEndian( block );
		ulong _upper = BinaryPrimitives.ReadUInt64LittleEndian( block[8..] );

		public int Read( int count )
		{
			var value = (int)(_lower & ((1ul << count) - 1));
			_lower = (_lower >> count) | (_upper << (64 - count));
			_upper >>= count;
			return value;
		}
	}

	public static void ReadRgba( ReadOnlySpan<byte> source, Span<byte> target, int width, int height, bool hasAlpha, bool flipRows = false )
	{
		var bytesPerBlock = hasAlpha ? 16 : 8;
		var offset = 0;
		Span<Color32> colors = stackalloc Color32[4];
		Span<byte> alpha = stackalloc byte[8];
		for ( var y = 0; y < height; y += 4 )
		for ( var x = 0; x < width; x += 4 )
		{
			var block = source.Slice( offset, bytesPerBlock );
			offset += bytesPerBlock;
			var alphaIndices = 0UL;
			if ( hasAlpha )
			{
				ReadAlpha( block[0], block[1], alpha );
				alphaIndices = BinaryPrimitives.ReadUInt64LittleEndian( block ) >> 16;
				block = block[8..];
			}

			var first = BinaryPrimitives.ReadUInt16LittleEndian( block );
			var second = BinaryPrimitives.ReadUInt16LittleEndian( block[2..] );
			colors[0] = ReadRgb565( first );
			colors[1] = ReadRgb565( second );
			var fourColors = hasAlpha || first > second;
			colors[2] = Mix( colors[0], colors[1], fourColors ? 2 : 1 );
			colors[3] = fourColors ? Mix( colors[1], colors[0], 2 ) : default;
			var indices = BinaryPrimitives.ReadUInt32LittleEndian( block[4..] );
			for ( var row = 0; row < Math.Min( 4, height - y ); row++ )
			for ( var column = 0; column < Math.Min( 4, width - x ); column++ )
			{
				var pixel = row * 4 + column;
				var color = colors[(int)(indices >> (2 * pixel)) & 3];
				var targetRow = flipRows ? height - 1 - y - row : y + row;
				var destination = (targetRow * width + x + column) * 4;
				target[destination] = color.r;
				target[destination + 1] = color.g;
				target[destination + 2] = color.b;
				target[destination + 3] = hasAlpha ? alpha[(int)(alphaIndices >> (3 * pixel)) & 7] : color.a;
			}
		}
	}

	public static void ReadRg( ReadOnlySpan<byte> source, Span<byte> target, int width, int height, bool hasGreen, bool flipRows )
	{
		var blockSize = hasGreen ? 16 : 8;
		var offset = 0;
		Span<byte> red = stackalloc byte[8];
		Span<byte> green = stackalloc byte[8];
		green.Clear();
		for ( var y = 0; y < height; y += 4 )
		for ( var x = 0; x < width; x += 4 )
		{
			var block = source.Slice( offset, blockSize );
			offset += blockSize;
			ReadAlpha( block[0], block[1], red );
			var redIndices = BinaryPrimitives.ReadUInt64LittleEndian( block ) >> 16;
			var greenIndices = 0ul;
			if ( hasGreen )
			{
				ReadAlpha( block[8], block[9], green );
				greenIndices = BinaryPrimitives.ReadUInt64LittleEndian( block[8..] ) >> 16;
			}
			for ( var row = 0; row < Math.Min( 4, height - y ); row++ )
			for ( var column = 0; column < Math.Min( 4, width - x ); column++ )
			{
				var shift = (row * 4 + column) * 3;
				var targetRow = flipRows ? height - 1 - y - row : y + row;
				var destination = (targetRow * width + x + column) * 4;
				target[destination] = red[(int)(redIndices >> shift) & 7];
				target[destination + 1] = green[(int)(greenIndices >> shift) & 7];
				target[destination + 2] = 0;
				target[destination + 3] = 255;
			}
		}
	}

	public static void ReadAlpha( byte first, byte second, Span<byte> values )
	{
		values[0] = first;
		values[1] = second;
		var steps = first > second ? 7 : 5;
		for ( var i = 1; i < steps; i++ )
			values[i + 1] = (byte)(((steps - i) * first + i * second) / steps);
		if ( steps == 5 )
		{
			values[6] = 0;
			values[7] = 255;
		}
	}

	static Color32 ReadRgb565( ushort value )
	{
		var red = value >> 11;
		var green = (value >> 5) & 63;
		var blue = value & 31;
		return new( (byte)((red << 3) | (red >> 2)), (byte)((green << 2) | (green >> 4)), (byte)((blue << 3) | (blue >> 2)), 255 );
	}

	static Color32 Mix( Color32 a, Color32 b, int weight ) =>
		new( (byte)((weight * a.r + b.r) / (weight + 1)), (byte)((weight * a.g + b.g) / (weight + 1)),
			(byte)((weight * a.b + b.b) / (weight + 1)), 255 );
}

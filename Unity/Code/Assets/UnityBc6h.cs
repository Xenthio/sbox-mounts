using System.Buffers.Binary;

namespace UnityMount;

/// <summary>Decodes Unity's unsigned BC6H textures into half-float pixels without a GPU.</summary>
static partial class UnityBc6h
{
	readonly record struct Field( int Endpoint, int Width, int Shift = 0, bool Reverse = false );
	readonly record struct Partition( ushort SubsetMask, int SecondAnchor );
	readonly record struct Mode( int Encoding, int BaseBits, int RedDelta, int GreenDelta, int BlueDelta, Field[] Fields );

	const int R0 = 0, G0 = 1, B0 = 2;
	const int R1 = 3, G1 = 4, B1 = 5;
	const int R2 = 6, G2 = 7, B2 = 8;
	const int R3 = 9, G3 = 10, B3 = 11;

	static ReadOnlySpan<byte> Weights3 => [0, 9, 18, 27, 37, 46, 55, 64];
	static ReadOnlySpan<byte> Weights4 => [0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64];

	public static void ReadRgbaHalf( ReadOnlySpan<byte> source, Span<byte> target, int size )
	{
		Span<ushort> colors = stackalloc ushort[16 * 3];
		var offset = 0;
		for ( var y = 0; y < size; y += 4 )
		for ( var x = 0; x < size; x += 4 )
		{
			DecodeBlock( source.Slice( offset, 16 ), colors );
			offset += 16;
			for ( var row = 0; row < Math.Min( 4, size - y ); row++ )
			for ( var column = 0; column < Math.Min( 4, size - x ); column++ )
			{
				var pixel = (row * 4 + column) * 3;
				var output = target.Slice( ((y + row) * size + x + column) * 8, 8 );
				BinaryPrimitives.WriteUInt16LittleEndian( output, colors[pixel] );
				BinaryPrimitives.WriteUInt16LittleEndian( output[2..], colors[pixel + 1] );
				BinaryPrimitives.WriteUInt16LittleEndian( output[4..], colors[pixel + 2] );
				BinaryPrimitives.WriteUInt16LittleEndian( output[6..], 0x3c00 ); // Half-float 1.
			}
		}
	}

	static void DecodeBlock( ReadOnlySpan<byte> block, Span<ushort> output )
	{
		var bits = new UnityBlockTexture.BitReader( block );
		var encoding = bits.Read( 2 );
		if ( encoding > 1 ) encoding |= bits.Read( 3 ) << 2;
		if ( !ModesByEncoding.TryGetValue( encoding, out var mode ) )
		{
			output.Clear();
			return;
		}

		Span<int> endpoints = stackalloc int[4 * 3];
		endpoints.Clear();
		foreach ( var field in mode.Fields )
		{
			var value = bits.Read( field.Width );
			if ( field.Reverse ) value = ReverseBits( value, field.Width );
			endpoints[field.Endpoint] |= value << field.Shift;
		}

		var partitioned = (encoding & 3) != 3;
		var partition = partitioned ? Partitions[bits.Read( 5 )] : default;
		var endpointCount = partitioned ? 4 : 2;
		if ( encoding is not (0b11110 or 0b00011) )
		{
			var mask = (1 << mode.BaseBits) - 1;
			for ( var i = 1; i < endpointCount; i++ )
			{
				var endpoint = endpoints.Slice( i * 3, 3 );
				endpoint[0] = (endpoints[0] + SignExtend( endpoint[0], mode.RedDelta )) & mask;
				endpoint[1] = (endpoints[1] + SignExtend( endpoint[1], mode.GreenDelta )) & mask;
				endpoint[2] = (endpoints[2] + SignExtend( endpoint[2], mode.BlueDelta )) & mask;
			}
		}
		for ( var i = 0; i < endpointCount * 3; i++ )
			endpoints[i] = Unquantize( endpoints[i], mode.BaseBits );

		var weights = partitioned ? Weights3 : Weights4;
		var indexWidth = partitioned ? 3 : 4;
		for ( var pixel = 0; pixel < 16; pixel++ )
		{
			var anchor = pixel == 0 || partitioned && pixel == partition.SecondAnchor;
			var weight = weights[bits.Read( anchor ? indexWidth - 1 : indexWidth )];
			var pair = ((partition.SubsetMask >> pixel) & 1) * 6;
			for ( var channel = 0; channel < 3; channel++ )
			{
				var first = endpoints[pair + channel];
				var second = endpoints[pair + channel + 3];
				var sample = (first * (64 - weight) + second * weight + 32) >> 6;
				output[pixel * 3 + channel] = (ushort)((sample * 31) >> 6);
			}
		}
	}

	static int ReverseBits( int value, int width )
	{
		var reversed = 0;
		for ( var i = 0; i < width; i++ )
		{
			reversed = (reversed << 1) | (value & 1);
			value >>= 1;
		}
		return reversed;
	}

	static int SignExtend( int value, int width ) => (value << (32 - width)) >> (32 - width);

	static int Unquantize( int value, int width )
	{
		if ( width >= 15 || value == 0 ) return value;
		if ( value == (1 << width) - 1 ) return 0xffff;
		return ((value << 16) + 0x8000) >> width;
	}

}

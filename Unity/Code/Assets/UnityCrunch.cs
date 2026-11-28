using System.Buffers.Binary;
using static UnityMount.UnityCrunchCoding;

namespace UnityMount;

/// <summary>Transcodes Unity Crunch textures to their original BC1 or BC3 blocks.</summary>
// Managed port of the Unity Crunch transcoder. See ThirdParty/crunch.txt.
static class UnityCrunch
{
	const int MipTableOffset = 70;
	readonly record struct Palette( int Offset, int Length, int Count );
	readonly record struct Channel( uint[] Endpoints, ulong[] Selectors, Huffman EndpointDeltas, Huffman SelectorIndices );
	readonly record struct BlockReference( int Mode, int Color, int Alpha );

	public static byte[] Decode( ReadOnlySpan<byte> data, int width, int height, int mips, bool hasAlpha, int decodedSize )
	{
		var tableEnd = MipTableOffset + mips * 4;
		if ( data.Length < tableEnd || U16( data ) != 0x4878 || U32( data[6..] ) != data.Length ||
			U16( data[12..] ) != width || U16( data[14..] ) != height || data[16] != mips ||
			data[17] != 1 || data[18] != (hasAlpha ? 2 : 0) || (U16( data[19..] ) & 1) != 0 )
			throw new InvalidDataException( "Invalid Unity Crunch texture header." );

		var headerLength = U16( data[2..] );
		if ( headerLength < tableEnd || headerLength > data.Length )
			throw new InvalidDataException( "Invalid Unity Crunch header length." );

		var firstMip = (int)U32( data[MipTableOffset..] );
		if ( firstMip < headerLength || firstMip >= data.Length )
			throw new InvalidDataException( "Invalid Unity Crunch first mip offset." );

		var palettes = data.Slice( headerLength, firstMip - headerLength );
		var tables = new Bits( ReadSection( palettes, U24( data[67..] ) - headerLength, U16( data[65..] ) ) );
		var references = Huffman.Read( ref tables );
		var color = ReadChannel( palettes, ReadPalette( data[33..] ), ReadPalette( data[41..] ), headerLength, false, ref tables );
		var alpha = hasAlpha
			? ReadChannel( palettes, ReadPalette( data[49..] ), ReadPalette( data[57..] ), headerLength, true, ref tables )
			: default;

		var output = new byte[decodedSize];
		var outputOffset = 0;
		var sourceOffset = firstMip;
		var blockSize = hasAlpha ? 16 : 8;
		for ( var mip = 0; mip < mips; mip++ )
		{
			var sourceEnd = mip + 1 == mips ? data.Length : (int)U32( data[(MipTableOffset + (mip + 1) * 4)..] );
			if ( sourceEnd <= sourceOffset || sourceEnd > data.Length )
				throw new InvalidDataException( "Invalid Unity Crunch mip range." );

			var blocksWide = (Math.Max( 1, width >> mip ) + 3) / 4;
			var blocksHigh = (Math.Max( 1, height >> mip ) + 3) / 4;
			var length = blocksWide * blocksHigh * blockSize;
			DecodeMip( data.Slice( sourceOffset, sourceEnd - sourceOffset ), output.AsSpan( outputOffset, length ),
				blocksWide, blocksHigh, references, color, alpha, hasAlpha );
			sourceOffset = sourceEnd;
			outputOffset += length;
		}
		return output;
	}

	static Channel ReadChannel( ReadOnlySpan<byte> data, Palette endpoints, Palette selectors, int headerLength,
		bool alpha, ref Bits tables )
	{
		if ( endpoints.Count == 0 || selectors.Count == 0 )
			throw new InvalidDataException( "Missing Unity Crunch palette." );

		var endpointDeltas = Huffman.Read( ref tables );
		var selectorIndices = Huffman.Read( ref tables );
		var endpointReader = new Bits( ReadSection( data, endpoints.Offset - headerLength, endpoints.Length ) );
		var selectorReader = new Bits( ReadSection( data, selectors.Offset - headerLength, selectors.Length ) );
		return new( ReadEndpoints( ref endpointReader, endpoints.Count, alpha ), ReadSelectors( ref selectorReader, selectors.Count, alpha ),
			endpointDeltas, selectorIndices );
	}

	static uint[] ReadEndpoints( ref Bits bits, int count, bool alpha )
	{
		var first = Huffman.Read( ref bits );
		var green = alpha ? null : Huffman.Read( ref bits );
		var data = new uint[count];
		Span<int> previous = stackalloc int[6];
		previous.Clear();

		for ( var i = 0; i < count; i++ )
		{
			if ( alpha )
			{
				for ( var component = 0; component < 2; component++ )
					previous[component] = (previous[component] + first.Decode( ref bits )) & 255;
				data[i] = (uint)(previous[0] | previous[1] << 8);
				continue;
			}

			for ( var component = 0; component < 6; component++ )
			{
				var isGreen = component % 3 == 1;
				var delta = (isGreen ? green : first).Decode( ref bits );
				previous[component] = (previous[component] + delta) & (isGreen ? 63 : 31);
			}
			data[i] = (uint)(previous[2] | previous[1] << 5 | previous[0] << 11 |
				previous[5] << 16 | previous[4] << 21 | previous[3] << 27);
		}
		return data;
	}

	static ulong[] ReadSelectors( ref Bits bits, int count, bool alpha )
	{
		var decoder = Huffman.Read( ref bits );
		var data = new ulong[count];
		var previous = 0ul;
		var width = alpha ? 3 : 2;
		ReadOnlySpan<byte> order = alpha ? [0, 2, 3, 4, 5, 6, 7, 1] : [0, 2, 3, 1];

		for ( var i = 0; i < count; i++ )
		{
			for ( var shift = 0; shift < width * 16; shift += width * 2 )
			{
				var delta = decoder.Decode( ref bits );
				if ( delta >= 1 << (width * 2) ) throw new InvalidDataException( "Invalid Unity Crunch selector delta." );
				previous ^= (ulong)delta << shift;
			}
			var selector = 0ul;
			for ( var shift = 0; shift < width * 16; shift += width )
				selector |= (ulong)order[(int)(previous >> shift) & (order.Length - 1)] << shift;
			data[i] = selector;
		}
		return data;
	}

	static void DecodeMip( ReadOnlySpan<byte> source, Span<byte> output, int width, int height,
		Huffman references, Channel color, Channel alpha, bool hasAlpha )
	{
		var bits = new Bits( source );
		// Endpoint references are stored in groups of 2x2 blocks, including edge padding.
		var paddedWidth = (width + 1) & ~1;
		var paddedHeight = (height + 1) & ~1;
		var row = new BlockReference[paddedWidth];
		var group = 0;
		var colorIndex = 0;
		var alphaIndex = 0;
		var blockSize = hasAlpha ? 16 : 8;
		for ( var y = 0; y < paddedHeight; y++ )
		for ( var x = 0; x < paddedWidth; x++ )
		{
			var above = row[x];
			var mode = above.Mode;
			var nextMode = mode;
			if ( (y & 1) == 0 )
			{
				if ( (x & 1) == 0 ) group = references.Decode( ref bits );
				mode = group & 3;
				nextMode = (group >> 2) & 3;
				group >>= 4;
			}

			switch ( mode )
			{
				case 0:
					colorIndex = ReadEndpointIndex( ref bits, color, colorIndex );
					if ( hasAlpha ) alphaIndex = ReadEndpointIndex( ref bits, alpha, alphaIndex );
					break;
				case 1:
					break;
				default:
					colorIndex = above.Color;
					alphaIndex = above.Alpha;
					break;
			}
			row[x] = new( nextMode, colorIndex, alphaIndex );

			var colorSelector = ReadSelector( ref bits, color );
			var alphaSelector = hasAlpha ? ReadSelector( ref bits, alpha ) : 0ul;
			if ( x >= width || y >= height ) continue;

			var block = output.Slice( (y * width + x) * blockSize, blockSize );
			if ( hasAlpha )
			{
				BinaryPrimitives.WriteUInt64LittleEndian( block, alpha.Endpoints[alphaIndex] | alphaSelector << 16 );
				block = block[8..];
			}
			BinaryPrimitives.WriteUInt32LittleEndian( block, color.Endpoints[colorIndex] );
			BinaryPrimitives.WriteUInt32LittleEndian( block[4..], (uint)colorSelector );
		}
	}

	static int ReadEndpointIndex( ref Bits bits, Channel channel, int index )
	{
		var delta = channel.EndpointDeltas.Decode( ref bits );
		if ( delta >= channel.Endpoints.Length ) throw new InvalidDataException( "Invalid Unity Crunch endpoint delta." );
		return (index + delta) % channel.Endpoints.Length;
	}

	static ulong ReadSelector( ref Bits bits, Channel channel )
	{
		var index = channel.SelectorIndices.Decode( ref bits );
		if ( index >= channel.Selectors.Length ) throw new InvalidDataException( "Invalid Unity Crunch selector index." );
		return channel.Selectors[index];
	}

	static ReadOnlySpan<byte> ReadSection( ReadOnlySpan<byte> input, int offset, int length )
	{
		if ( offset < 0 || length <= 0 || offset > input.Length || length > input.Length - offset )
			throw new InvalidDataException( "Invalid Unity Crunch section range." );
		return input.Slice( offset, length );
	}

	static Palette ReadPalette( ReadOnlySpan<byte> data ) => new( U24( data ), U24( data[3..] ), U16( data[6..] ) );
	static ushort U16( ReadOnlySpan<byte> data ) => BinaryPrimitives.ReadUInt16BigEndian( data );
	static int U24( ReadOnlySpan<byte> data ) => (data[0] << 16) | U16( data[1..] );
	static uint U32( ReadOnlySpan<byte> data ) => BinaryPrimitives.ReadUInt32BigEndian( data );
}

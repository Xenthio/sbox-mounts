using System.Numerics;

namespace UnityMount;

/// <summary>Decodes BC7 blocks before the upload changes texture orientation or packs normals.</summary>
static class UnityBc7
{
	enum PBits
	{
		None,
		PerSubset,
		PerEndpoint
	}

	readonly record struct Mode( int Subsets, int PartitionBits, int ColorBits, int AlphaBits, PBits PBits,
		int IndexBits, int SecondaryIndexBits = 0 );
	readonly record struct Partition( uint Subsets, ushort Anchors )
	{
		public int SubsetFor( int texel ) => (int)(Subsets >> (texel * 2)) & 3;
		public bool IsAnchor( int texel ) => (Anchors & (1 << texel)) != 0;
	}

	// Modes are selected by the position of the first set bit in a block.
	static readonly Mode[] Modes =
	[
		new( 3, 4, 4, 0, PBits.PerEndpoint, 3 ),
		new( 2, 6, 6, 0, PBits.PerSubset, 3 ),
		new( 3, 6, 5, 0, PBits.None, 2 ),
		new( 2, 6, 7, 0, PBits.PerEndpoint, 2 ),
		new( 1, 0, 5, 6, PBits.None, 2, 3 ),
		new( 1, 0, 7, 8, PBits.None, 2, 2 ),
		new( 1, 0, 7, 7, PBits.PerEndpoint, 4 ),
		new( 2, 6, 5, 5, PBits.PerEndpoint, 2 )
	];

	public static void ReadRgba( ReadOnlySpan<byte> source, Span<byte> target, int width, int height, bool flipRows )
	{
		Span<byte> texels = stackalloc byte[16 * 4];
		var offset = 0;
		for ( var y = 0; y < height; y += 4 )
		for ( var x = 0; x < width; x += 4 )
		{
			DecodeBlock( source.Slice( offset, 16 ), texels );
			offset += 16;
			var rowBytes = Math.Min( 4, width - x ) * 4;
			for ( var row = 0; row < Math.Min( 4, height - y ); row++ )
			{
				var targetRow = flipRows ? height - 1 - y - row : y + row;
				texels.Slice( row * 16, rowBytes ).CopyTo( target.Slice( (targetRow * width + x) * 4, rowBytes ) );
			}
		}
	}

	static void DecodeBlock( ReadOnlySpan<byte> block, Span<byte> output )
	{
		var encoding = BitOperations.TrailingZeroCount( (uint)block[0] );
		if ( encoding >= Modes.Length )
		{
			output.Clear();
			return;
		}

		var mode = Modes[encoding];
		var bits = new UnityBlockTexture.BitReader( block );
		bits.Read( encoding + 1 );
		var partition = mode.Subsets switch
		{
			2 => TwoSubsets[bits.Read( mode.PartitionBits )],
			3 => ThreeSubsets[bits.Read( mode.PartitionBits )],
			_ => new Partition( 0, 1 )
		};
		var rotation = mode.SecondaryIndexBits > 0 ? bits.Read( 2 ) : 0;
		var swapIndices = encoding == 4 && bits.Read( 1 ) != 0;

		var endpointCount = mode.Subsets * 2;
		Span<int> endpoints = stackalloc int[6 * 4];
		for ( var channel = 0; channel < 4; channel++ )
		{
			var precision = channel == 3 ? mode.AlphaBits : mode.ColorBits;
			for ( var endpoint = 0; endpoint < endpointCount; endpoint++ )
				endpoints[endpoint * 4 + channel] = precision == 0 ? 0 : bits.Read( precision );
		}

		var pBit = 0;
		var hasPBits = mode.PBits != PBits.None;
		for ( var endpoint = 0; endpoint < endpointCount; endpoint++ )
		{
			if ( mode.PBits == PBits.PerEndpoint || mode.PBits == PBits.PerSubset && endpoint % 2 == 0 )
				pBit = bits.Read( 1 );

			for ( var channel = 0; channel < 4; channel++ )
			{
				var precision = channel == 3 ? mode.AlphaBits : mode.ColorBits;
				var index = endpoint * 4 + channel;
				if ( precision == 0 )
				{
					endpoints[index] = 255;
					continue;
				}

				var value = endpoints[index];
				if ( hasPBits )
				{
					value = (value << 1) | pBit;
					precision++;
				}
				value <<= 8 - precision;
				endpoints[index] = value | (value >> precision);
			}
		}

		var primaryWeights = Weights( mode.IndexBits );
		var secondaryWeights = Weights( mode.SecondaryIndexBits );
		Span<int> weights = stackalloc int[16];
		for ( var texel = 0; texel < 16; texel++ )
			weights[texel] = primaryWeights[bits.Read( mode.IndexBits - (partition.IsAnchor( texel ) ? 1 : 0) )];

		for ( var texel = 0; texel < 16; texel++ )
		{
			var primary = weights[texel];
			var secondary = mode.SecondaryIndexBits == 0 ? primary
				: secondaryWeights[bits.Read( mode.SecondaryIndexBits - (texel == 0 ? 1 : 0) )];
			var colorWeight = swapIndices ? secondary : primary;
			var alphaWeight = swapIndices ? primary : secondary;
			var pair = partition.SubsetFor( texel ) * 8;
			var pixel = output.Slice( texel * 4, 4 );
			for ( var channel = 0; channel < 4; channel++ )
			{
				var weight = channel == 3 ? alphaWeight : colorWeight;
				var first = endpoints[pair + channel];
				var second = endpoints[pair + channel + 4];
				pixel[channel] = (byte)((first * (64 - weight) + second * weight + 32) >> 6);
			}
			// The scalar channel is encoded as alpha, then swapped back to its original channel.
			if ( rotation > 0 ) (pixel[3], pixel[rotation - 1]) = (pixel[rotation - 1], pixel[3]);
		}
	}

	static ReadOnlySpan<byte> Weights( int bits ) => bits switch
	{
		2 => [0, 21, 43, 64],
		3 => [0, 9, 18, 27, 37, 46, 55, 64],
		4 => [0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64],
		_ => []
	};

	// Partition and anchor tables packed from bcdec. See Unity/ThirdParty/bcdec.txt.
	static readonly Partition[] TwoSubsets =
	[
		new( 0x50505050, 0x8001 ), new( 0x40404040, 0x8001 ),
		new( 0x54545454, 0x8001 ), new( 0x54505040, 0x8001 ),
		new( 0x50404000, 0x8001 ), new( 0x55545450, 0x8001 ),
		new( 0x55545040, 0x8001 ), new( 0x54504000, 0x8001 ),
		new( 0x50400000, 0x8001 ), new( 0x55555450, 0x8001 ),
		new( 0x55544000, 0x8001 ), new( 0x54400000, 0x8001 ),
		new( 0x55555440, 0x8001 ), new( 0x55550000, 0x8001 ),
		new( 0x55555500, 0x8001 ), new( 0x55000000, 0x8001 ),
		new( 0x55150100, 0x8001 ), new( 0x00004054, 0x0005 ),
		new( 0x15010000, 0x0101 ), new( 0x00405054, 0x0005 ),
		new( 0x00004050, 0x0005 ), new( 0x15050100, 0x0101 ),
		new( 0x05010000, 0x0101 ), new( 0x40505054, 0x8001 ),
		new( 0x00404050, 0x0005 ), new( 0x05010100, 0x0101 ),
		new( 0x14141414, 0x0005 ), new( 0x05141450, 0x0005 ),
		new( 0x01155440, 0x0101 ), new( 0x00555500, 0x0101 ),
		new( 0x15014054, 0x0005 ), new( 0x05414150, 0x0005 ),
		new( 0x44444444, 0x8001 ), new( 0x55005500, 0x8001 ),
		new( 0x11441144, 0x0041 ), new( 0x05055050, 0x0101 ),
		new( 0x05500550, 0x0005 ), new( 0x11114444, 0x0101 ),
		new( 0x41144114, 0x8001 ), new( 0x44111144, 0x8001 ),
		new( 0x15055054, 0x0005 ), new( 0x01055040, 0x0101 ),
		new( 0x05041050, 0x0005 ), new( 0x05455150, 0x0005 ),
		new( 0x14414114, 0x0005 ), new( 0x50050550, 0x8001 ),
		new( 0x41411414, 0x8001 ), new( 0x00141400, 0x0041 ),
		new( 0x00041504, 0x0041 ), new( 0x00105410, 0x0005 ),
		new( 0x10541000, 0x0041 ), new( 0x04150400, 0x0101 ),
		new( 0x50410514, 0x8001 ), new( 0x41051450, 0x8001 ),
		new( 0x05415014, 0x0005 ), new( 0x14054150, 0x0005 ),
		new( 0x41050514, 0x8001 ), new( 0x41505014, 0x8001 ),
		new( 0x40011554, 0x8001 ), new( 0x54150140, 0x8001 ),
		new( 0x50505500, 0x8001 ), new( 0x00555050, 0x0005 ),
		new( 0x15151010, 0x0005 ), new( 0x54540404, 0x8001 ),
	];
	static readonly Partition[] ThreeSubsets =
	[
		new( 0xaa685050, 0x8009 ), new( 0x6a5a5040, 0x0109 ),
		new( 0x5a5a4200, 0x8101 ), new( 0x5450a0a8, 0x8009 ),
		new( 0xa5a50000, 0x8101 ), new( 0xa0a05050, 0x8009 ),
		new( 0x5555a0a0, 0x8009 ), new( 0x5a5a5050, 0x8101 ),
		new( 0xaa550000, 0x8101 ), new( 0xaa555500, 0x8101 ),
		new( 0xaaaa5500, 0x8041 ), new( 0x90909090, 0x8041 ),
		new( 0x94949494, 0x8041 ), new( 0xa4a4a4a4, 0x8021 ),
		new( 0xa9a59450, 0x8009 ), new( 0x2a0a4250, 0x0109 ),
		new( 0xa5945040, 0x8009 ), new( 0x0a425054, 0x0109 ),
		new( 0xa5a5a500, 0x8101 ), new( 0x55a0a0a0, 0x8009 ),
		new( 0xa8a85454, 0x8009 ), new( 0x6a6a4040, 0x0109 ),
		new( 0xa4a45000, 0x8041 ), new( 0x1a1a0500, 0x0501 ),
		new( 0x0050a4a4, 0x0029 ), new( 0xaaa59090, 0x8101 ),
		new( 0x14696914, 0x0141 ), new( 0x69691400, 0x0441 ),
		new( 0xa08585a0, 0x8101 ), new( 0xaa821414, 0x8021 ),
		new( 0x50a4a450, 0x8401 ), new( 0x6a5a0200, 0x8101 ),
		new( 0xa9a58000, 0x8101 ), new( 0x5090a0a8, 0x8009 ),
		new( 0xa8a09050, 0x8009 ), new( 0x24242424, 0x0421 ),
		new( 0x00aa5500, 0x0441 ), new( 0x24924924, 0x0501 ),
		new( 0x24499224, 0x0301 ), new( 0x50a50a50, 0x8401 ),
		new( 0x500aa550, 0x8041 ), new( 0xaaaa4444, 0x8009 ),
		new( 0x66660000, 0x8101 ), new( 0xa5a0a5a0, 0x8021 ),
		new( 0x50a050a0, 0x8009 ), new( 0x69286928, 0x8041 ),
		new( 0x44aaaa44, 0x8041 ), new( 0x66666600, 0x8101 ),
		new( 0xaa444444, 0x8009 ), new( 0x54a854a8, 0x8009 ),
		new( 0x95809580, 0x8021 ), new( 0x96969600, 0x8021 ),
		new( 0xa85454a8, 0x8021 ), new( 0x80959580, 0x8101 ),
		new( 0xaa141414, 0x8021 ), new( 0x96960000, 0x8401 ),
		new( 0xaaaa1414, 0x8021 ), new( 0xa05050a0, 0x8401 ),
		new( 0xa0a5a5a0, 0x8101 ), new( 0x96000000, 0xa001 ),
		new( 0x40804080, 0x8009 ), new( 0xa9a8a9a8, 0x9001 ),
		new( 0xaaaaaa44, 0x8009 ), new( 0x2a4a5254, 0x0109 ),
	];
}

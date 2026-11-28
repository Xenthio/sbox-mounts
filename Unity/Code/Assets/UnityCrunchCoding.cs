namespace UnityMount;

// Managed port of the Unity Crunch Huffman streams. See ThirdParty/crunch.txt.
static class UnityCrunchCoding
{
	public ref struct Bits( ReadOnlySpan<byte> data )
	{
		readonly ReadOnlySpan<byte> _data = data;
		int _offset;
		int _available;
		uint _buffer;

		public int Read( int count )
		{
			while ( _available < count )
			{
				if ( _offset >= _data.Length ) throw new InvalidDataException( "Truncated Unity Crunch stream." );
				_buffer = (_buffer << 8) | _data[_offset++];
				_available += 8;
			}
			_available -= count;
			return (int)(_buffer >> _available) & ((1 << count) - 1);
		}
	}

	public sealed class Huffman
	{
		const int MaxBits = 16;
		static ReadOnlySpan<byte> LengthOrder => [17, 18, 19, 20, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15, 16];

		readonly int[] _counts = new int[MaxBits + 1];
		readonly int[] _firstCode = new int[MaxBits + 1];
		readonly int[] _firstSymbol = new int[MaxBits + 1];
		readonly ushort[] _symbols;
		readonly int _maxLength;

		Huffman( ReadOnlySpan<byte> lengths )
		{
			foreach ( var length in lengths )
			{
				if ( length == 0 ) continue;
				_counts[length]++;
				_maxLength = Math.Max( _maxLength, length );
			}

			var code = 0;
			var total = 0;
			for ( var length = 1; length <= _maxLength; length++ )
			{
				code = (code + _counts[length - 1]) << 1;
				if ( code + _counts[length] > 1 << length )
					throw new InvalidDataException( "Oversubscribed Unity Crunch Huffman model." );
				_firstCode[length] = code;
				_firstSymbol[length] = total;
				total += _counts[length];
			}
			_symbols = new ushort[total];
			Span<int> next = stackalloc int[MaxBits + 1];
			_firstSymbol.AsSpan().CopyTo( next );
			for ( var i = 0; i < lengths.Length; i++ )
			{
				var length = lengths[i];
				if ( length != 0 ) _symbols[next[length]++] = (ushort)i;
			}
		}

		public static Huffman Read( ref Bits bits )
		{
			const int maxSymbols = 8192;
			var symbolCount = bits.Read( 14 );
			if ( symbolCount == 0 ) return new Huffman( [] );
			if ( symbolCount > maxSymbols ) throw new InvalidDataException( "Invalid Unity Crunch Huffman symbol count." );

			var lengthCount = bits.Read( 5 );
			if ( lengthCount < 1 || lengthCount > LengthOrder.Length )
				throw new InvalidDataException( "Invalid Unity Crunch Huffman length count." );
			Span<byte> lengthModel = stackalloc byte[21];
			lengthModel.Clear();
			foreach ( var index in LengthOrder[..lengthCount] )
				lengthModel[index] = (byte)bits.Read( 3 );
			var decoder = new Huffman( lengthModel );

			var lengths = new byte[symbolCount];
			var offset = 0;
			while ( offset < lengths.Length )
			{
				var symbol = decoder.Decode( ref bits );
				if ( symbol <= MaxBits )
				{
					lengths[offset++] = (byte)symbol;
					continue;
				}

				var count = symbol switch
				{
					17 => 3 + bits.Read( 3 ),
					18 => 11 + bits.Read( 7 ),
					19 => 3 + bits.Read( 2 ),
					_ => 7 + bits.Read( 6 )
				};
				if ( count > lengths.Length - offset || (symbol >= 19 && (offset == 0 || lengths[offset - 1] == 0)) )
					throw new InvalidDataException( "Invalid Unity Crunch Huffman length run." );
				if ( symbol >= 19 ) lengths.AsSpan( offset, count ).Fill( lengths[offset - 1] );
				offset += count;
			}
			return new Huffman( lengths );
		}

		public int Decode( ref Bits bits )
		{
			var code = 0;
			for ( var length = 1; length <= _maxLength; length++ )
			{
				code = (code << 1) | bits.Read( 1 );
				var offset = code - _firstCode[length];
				if ( (uint)offset < _counts[length] ) return _symbols[_firstSymbol[length] + offset];
			}
			throw new InvalidDataException( "Invalid Unity Crunch Huffman code." );
		}
	}
}

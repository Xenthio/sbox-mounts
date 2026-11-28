using System.Buffers.Binary;

namespace UnityMount.Wwise;

/// <summary>Expands packed Wwise Vorbis codebooks into Vorbis I setup data.</summary>
sealed class VorbisCodebooks
{
	readonly byte[] _data;
	readonly int[] _offsets;

	public VorbisCodebooks( byte[] data )
	{
		_data = data;
		var indexStart = BinaryPrimitives.ReadInt32LittleEndian( data.AsSpan( data.Length - 4 ) );
		if ( indexStart <= 0 || indexStart > data.Length - 8 || (data.Length - indexStart) % 4 != 0 )
			throw new InvalidDataException( "Invalid Wwise codebook index." );

		_offsets = new int[(data.Length - indexStart) / 4];
		for ( var i = 0; i < _offsets.Length; i++ )
		{
			var offset = BinaryPrimitives.ReadInt32LittleEndian( data.AsSpan( indexStart + i * 4 ) );
			if ( offset < 0 || offset > indexStart || i > 0 && offset <= _offsets[i - 1] )
				throw new InvalidDataException( "Invalid Wwise codebook offset." );
			_offsets[i] = offset;
		}
	}

	// Packed codebook layout and reconstruction follow ww2ogg. See ThirdParty/ww2ogg.txt.
	public void Write( uint id, VorbisPacket output )
	{
		if ( id >= _offsets.Length - 1 ) throw new InvalidDataException( $"Unknown Wwise Vorbis codebook {id}." );
		var input = new VorbisBits( _data.AsSpan( _offsets[id], _offsets[id + 1] - _offsets[id] ) );
		var dimensions = input.Read( 4 );
		var entries = input.Read( 14 );
		if ( dimensions == 0 || entries == 0 ) throw new InvalidDataException( "Empty Wwise codebook." );

		output.Write( 0x564342, 24 );
		output.Write( dimensions, 16 );
		output.Write( entries, 24 );

		if ( input.Copy( output, 1 ) != 0 )
		{
			var length = input.Copy( output, 5 ) + 1;
			uint entry = 0;
			while ( entry < entries )
			{
				if ( length++ > 32 ) throw new InvalidDataException( "Invalid Wwise code length." );
				entry += input.Copy( output, VorbisBits.Width( entries - entry ) );
			}
			if ( entry != entries ) throw new InvalidDataException( "Invalid Wwise codebook entry count." );
		}
		else
		{
			var width = (int)input.Read( 3 );
			if ( width is < 1 or > 5 ) throw new InvalidDataException( "Invalid Wwise codeword width." );
			var sparse = input.Copy( output, 1 ) != 0;
			for ( var i = 0; i < entries; i++ )
				if ( !sparse || input.Copy( output, 1 ) != 0 ) output.Write( input.Read( width ), 5 );
		}

		var lookup = input.Read( 1 );
		output.Write( lookup, 4 );
		if ( lookup == 0 ) return;

		input.Copy( output, 32 ); // Minimum value.
		input.Copy( output, 32 ); // Delta value.
		var bits = (int)input.Copy( output, 4 ) + 1;
		input.Copy( output, 1 ); // Sequence flag.
		var values = (int)Math.Pow( entries, 1.0 / dimensions );
		while ( Math.Pow( values + 1, dimensions ) <= entries ) values++;
		while ( Math.Pow( values, dimensions ) > entries ) values--;
		for ( var i = 0; i < values; i++ ) input.Copy( output, bits );
	}
}

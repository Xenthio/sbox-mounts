namespace UnityMount.Wwise;

static class VorbisSetup
{
	// Reconstruct the Vorbis I fields omitted by Wwise. The packed layout follows ww2ogg.
	public static byte[] Rebuild( ReadOnlySpan<byte> packet, VorbisCodebooks books, int channels, out bool[] modes )
	{
		var input = new VorbisBits( packet );
		var output = new VorbisPacket( 5 );
		var bookCount = input.Copy( output, 8 ) + 1;
		for ( var i = 0; i < bookCount; i++ ) books.Write( input.Read( 10 ), output );

		output.Write( 0, 6 ); // One time-domain transform.
		output.Write( 0, 16 );

		var floorCount = input.Copy( output, 6 ) + 1;
		for ( var i = 0; i < floorCount; i++ ) WriteFloor( ref input, output );

		var residueCount = input.Copy( output, 6 ) + 1;
		for ( var i = 0; i < residueCount; i++ ) WriteResidue( ref input, output );

		var mappingCount = input.Copy( output, 6 ) + 1;
		for ( var i = 0; i < mappingCount; i++ ) WriteMapping( ref input, output, channels );

		modes = new bool[input.Copy( output, 6 ) + 1];
		for ( var i = 0; i < modes.Length; i++ )
		{
			modes[i] = input.Copy( output, 1 ) != 0;
			output.Write( 0, 32 ); // Window and transform types.
			input.Copy( output, 8 ); // Mapping.
		}

		if ( input.Remaining > 7 ) throw new InvalidDataException( "Unexpected data after Wwise Vorbis setup." );
		output.Write( 1, 1 );
		return output.Finish();
	}

	static void WriteFloor( ref VorbisBits input, VorbisPacket output )
	{
		output.Write( 1, 16 );
		Span<byte> partitions = stackalloc byte[(int)input.Copy( output, 5 )];
		Span<int> dimensions = stackalloc int[16];
		var maxClass = 0;
		for ( var i = 0; i < partitions.Length; i++ )
		{
			partitions[i] = (byte)input.Copy( output, 4 );
			maxClass = Math.Max( maxClass, partitions[i] );
		}

		for ( var i = 0; i <= maxClass; i++ )
		{
			dimensions[i] = (int)input.Copy( output, 3 ) + 1;
			var subclasses = (int)input.Copy( output, 2 );
			if ( subclasses > 0 ) input.Copy( output, 8 );
			for ( var j = 0; j < 1 << subclasses; j++ ) input.Copy( output, 8 );
		}

		input.Copy( output, 2 );
		var width = (int)input.Copy( output, 4 );
		foreach ( var partition in partitions )
			for ( var i = 0; i < dimensions[partition]; i++ ) input.Copy( output, width );
	}

	static void WriteResidue( ref VorbisBits input, VorbisPacket output )
	{
		output.Write( input.Read( 2 ), 16 );
		input.Copy( output, 24 );
		input.Copy( output, 24 );
		input.Copy( output, 24 );
		Span<uint> cascades = stackalloc uint[(int)input.Copy( output, 6 ) + 1];
		input.Copy( output, 8 );
		for ( var i = 0; i < cascades.Length; i++ )
		{
			var low = input.Copy( output, 3 );
			var high = input.Copy( output, 1 ) != 0 ? input.Copy( output, 5 ) : 0;
			cascades[i] = (high << 3) | low;
		}
		foreach ( var cascade in cascades )
			for ( var i = 0; i < 8; i++ )
				if ( (cascade & (1 << i)) != 0 ) input.Copy( output, 8 );
	}

	static void WriteMapping( ref VorbisBits input, VorbisPacket output, int channels )
	{
		output.Write( 0, 16 );
		var submaps = input.Copy( output, 1 ) != 0 ? (int)input.Copy( output, 4 ) + 1 : 1;
		if ( input.Copy( output, 1 ) != 0 )
		{
			var couplings = (int)input.Copy( output, 8 ) + 1;
			var width = VorbisBits.Width( (uint)(channels - 1) );
			for ( var i = 0; i < couplings; i++ )
			{
				input.Copy( output, width );
				input.Copy( output, width );
			}
		}

		input.Copy( output, 2 );
		if ( submaps > 1 )
			for ( var i = 0; i < channels; i++ ) input.Copy( output, 4 );
		for ( var i = 0; i < submaps; i++ ) input.Copy( output, 24 );
	}
}

namespace UnityMount.Wwise;

sealed class WwiseVorbis
{
	public const int MaxEncodedBytes = 32 * 1024 * 1024;
	const int MaxDecodedBytes = 256 * 1024 * 1024;
	static readonly Lazy<VorbisCodebooks> Codebooks = new( () =>
		new VorbisCodebooks( System.Convert.FromBase64String( VorbisCodebookData.AoTuV603 ) ) );

	public readonly record struct Audio( byte[] Data, int LoopStart, int LoopEnd, float Duration );
	readonly record struct Packet( int Offset, int Size, int Mode );

	public static Audio Convert( byte[] wem )
	{
		using var stream = new MemoryStream( wem, writable: false );
		using var reader = new UnityReader( stream );
		if ( reader.ReadUInt32() != 0x46464952 || reader.ReadUInt32() != wem.Length - 8 || reader.ReadUInt32() != 0x45564157 )
			throw new InvalidDataException( "Invalid Wwise RIFF header." );

		var format = (Offset: 0L, Size: 0);
		var data = (Offset: 0L, Size: 0);
		var loop = (Start: -1, End: -1);
		while ( reader.Remaining >= 8 )
		{
			var tag = reader.ReadUInt32();
			var length = reader.ReadInt32();
			var offset = reader.Position;
			if ( length < 0 || length > reader.Remaining ) throw new InvalidDataException( "Invalid Wwise RIFF chunk size." );
			switch ( tag )
			{
				case 0x20746d66: format = (offset, length); break; // fmt
				case 0x61746164: data = (offset, length); break; // data
				case 0x6c706d73 when length >= 52: // smpl
					reader.Position = offset + 28;
					if ( reader.ReadUInt32() == 1 )
					{
						reader.Position = offset + 44;
						loop = (reader.ReadInt32(), reader.ReadInt32());
					}
					break;
			}
			reader.Position = offset + length;
		}

		if ( format.Size != 0x42 || data.Size <= 0 )
			throw new NotSupportedException( "Wwise Vorbis requires the modern packed RIFF layout." );
		reader.Position = format.Offset;
		if ( reader.ReadUInt16() != 0xffff ) throw new NotSupportedException( "Unsupported Wwise RIFF codec." );
		var channels = reader.ReadUInt16();
		var rate = reader.ReadUInt32();
		if ( channels is < 1 or > 8 || rate is < 8000 or > 192000 )
			throw new InvalidDataException( "Invalid Wwise Vorbis channel count or sample rate." );
		var averageBytes = reader.ReadUInt32();
		var info = format.Offset + 0x18;
		reader.Position = info;
		var samples = reader.ReadInt32();
		if ( samples <= 0 || (long)samples * channels * sizeof( float ) > MaxDecodedBytes ) throw new InvalidDataException( "Invalid Wwise sample count." );
		var modified = reader.ReadUInt32() is not (0x4a or 0x4b or 0x69 or 0x70);
		reader.Position = info + 0x10;
		var setupOffset = reader.ReadInt32();
		var audioOffset = reader.ReadInt32();
		reader.Position = info + 0x28;
		var shortBlock = reader.ReadByte();
		var longBlock = reader.ReadByte();
		if ( shortBlock is < 6 or > 13 || longBlock < shortBlock || longBlock > 13 )
			throw new InvalidDataException( "Invalid Wwise Vorbis window sizes." );

		var payload = wem.AsSpan( (int)data.Offset, data.Size );
		var setupPacket = ReadPacket( payload, setupOffset );
		if ( setupPacket.Offset + setupPacket.Size != audioOffset )
			throw new InvalidDataException( "Invalid Wwise Vorbis audio offset." );
		var setup = VorbisSetup.Rebuild( payload.Slice( setupPacket.Offset, setupPacket.Size ), Codebooks.Value, channels, out var modes );
		var modeWidth = VorbisBits.Width( (uint)(modes.Length - 1) );

		var packets = new List<Packet>();
		var offsetInData = audioOffset;
		while ( offsetInData < payload.Length )
		{
			var packet = ReadPacket( payload, offsetInData );
			var input = new VorbisBits( payload.Slice( packet.Offset, packet.Size ) );
			if ( !modified && input.Read( 1 ) != 0 ) throw new InvalidDataException( "Invalid Vorbis audio packet type." );
			var mode = (int)input.Read( modeWidth );
			if ( mode >= modes.Length ) throw new InvalidDataException( "Invalid Vorbis audio packet mode." );
			packets.Add( new( packet.Offset, packet.Size, mode ) );
			offsetInData = packet.Offset + packet.Size;
		}
		if ( packets.Count == 0 ) throw new InvalidDataException( "Wwise Vorbis has no audio packets." );

		using var ogg = new MemoryStream();
		var output = new OggStream( ogg );
		var id = new VorbisPacket( 1 );
		id.Write( 0, 32 );
		id.Write( channels, 8 );
		id.Write( rate, 32 );
		id.Write( 0, 32 );
		id.Write( averageBytes <= int.MaxValue / 8 ? averageBytes * 8 : 0, 32 );
		id.Write( 0, 32 );
		id.Write( shortBlock, 4 );
		id.Write( longBlock, 4 );
		id.Write( 1, 8 );
		output.Write( id.Finish(), 0 );

		var comment = new VorbisPacket( 3 );
		comment.Write( 0, 32 ); // Empty vendor and comment list.
		comment.Write( 0, 32 );
		comment.Write( 1, 8 );
		output.Write( comment.Finish(), 0 );
		output.Write( setup, 0 );

		long granule = 0;
		var previousBlock = 0;
		for ( var i = 0; i < packets.Count; i++ )
		{
			var packet = packets[i];
			var dataBytes = payload.Slice( packet.Offset, packet.Size );
			var large = modes[packet.Mode];
			var block = 1 << (large ? longBlock : shortBlock);
			if ( i > 0 ) granule += (previousBlock + block) / 4;
			previousBlock = block;

			var final = i == packets.Count - 1;
			var bytes = modified
				? ExpandPacket( dataBytes, modeWidth, large, i > 0 && modes[packets[i - 1].Mode], !final && modes[packets[i + 1].Mode] )
				: dataBytes.ToArray();
			output.Write( bytes, final ? samples : Math.Min( granule, samples ), final );
		}

		var end = loop.End == 0 ? samples : (long)loop.End + 1;
		var duration = (float)samples / rate;
		return loop.Start >= 0 && end > loop.Start && end <= samples
			? new( ogg.ToArray(), loop.Start, (int)end, duration )
			: new( ogg.ToArray(), -1, -1, duration );
	}

	static (int Offset, int Size) ReadPacket( ReadOnlySpan<byte> data, int start )
	{
		if ( start < 0 || start > data.Length - 2 ) throw new InvalidDataException( "Invalid Wwise Vorbis packet offset." );
		var size = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian( data[start..] );
		var offset = start + 2;
		if ( size == 0 || size > data.Length - offset ) throw new InvalidDataException( "Truncated Wwise Vorbis packet." );
		return (offset, size);
	}

	static byte[] ExpandPacket( ReadOnlySpan<byte> data, int modeWidth, bool large, bool previous, bool next )
	{
		var input = new VorbisBits( data );
		var output = new VorbisPacket();
		output.Write( 0, 1 );
		input.Copy( output, modeWidth );
		if ( large )
		{
			output.Write( previous ? 1u : 0u, 1 );
			output.Write( next ? 1u : 0u, 1 );
		}
		while ( input.Remaining > 0 ) input.Copy( output, Math.Min( input.Remaining, 8 ) );
		return output.Finish();
	}
}

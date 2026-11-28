using System.Buffers.Binary;

namespace UnityMount.Wwise;

sealed class OggStream( Stream output )
{
	uint _pageIndex;

	public void Write( byte[] packet, long granule, bool final = false )
	{
		var segments = packet.Length / 255 + 1;
		if ( segments > 255 ) throw new InvalidDataException( "Vorbis packet exceeds one Ogg page." );

		const int headerSize = 27;
		var page = new byte[headerSize + segments + packet.Length];
		"OggS"u8.CopyTo( page );
		page[5] = (byte)(_pageIndex == 0 ? 2 : final ? 4 : 0);
		BinaryPrimitives.WriteInt64LittleEndian( page.AsSpan( 6 ), granule );
		BinaryPrimitives.WriteUInt32LittleEndian( page.AsSpan( 14 ), 1 ); // Stream serial.
		BinaryPrimitives.WriteUInt32LittleEndian( page.AsSpan( 18 ), _pageIndex++ );
		page[26] = (byte)segments;
		page.AsSpan( headerSize, segments - 1 ).Fill( 255 );
		page[headerSize + segments - 1] = (byte)(packet.Length % 255);
		packet.CopyTo( page, headerSize + segments );

		uint crc = 0;
		foreach ( var value in page )
		{
			crc ^= (uint)value << 24;
			for ( var bit = 0; bit < 8; bit++ )
				crc = (crc & 0x80000000) != 0 ? (crc << 1) ^ 0x04c11db7 : crc << 1;
		}
		BinaryPrimitives.WriteUInt32LittleEndian( page.AsSpan( 22 ), crc );
		output.Write( page );
	}
}

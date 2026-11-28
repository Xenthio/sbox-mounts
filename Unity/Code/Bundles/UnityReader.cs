using System.Buffers.Binary;
using System.Text;

namespace UnityMount;

sealed class UnityReader( Stream stream, bool leaveOpen = true ) : BinaryReader( stream, Encoding.UTF8, leaveOpen )
{
	public long Position { get => BaseStream.Position; set => BaseStream.Position = value; }
	public long Remaining => BaseStream.Length - Position;

	public ushort ReadBigUInt16() => BinaryPrimitives.ReverseEndianness( ReadUInt16() );
	public int ReadBigInt32() => BinaryPrimitives.ReverseEndianness( ReadInt32() );
	public long ReadBigInt64() => BinaryPrimitives.ReverseEndianness( ReadInt64() );

	public void Skip( long size )
	{
		if ( size < 0 || size > Remaining ) throw new EndOfStreamException();
		Position += size;
	}

	public void Align( int alignment ) => Skip( (-Position) & (alignment - 1) );

	public byte[] ReadExactBytes( int size )
	{
		if ( size < 0 || size > Remaining ) throw new EndOfStreamException();
		var bytes = new byte[size];
		BaseStream.ReadExactly( bytes );
		return bytes;
	}

	public string ReadNullTerminatedString()
	{
		using var bytes = new MemoryStream();
		while ( bytes.Length < 4096 )
		{
			var value = ReadByte();
			if ( value == 0 ) return Encoding.UTF8.GetString( bytes.ToArray() );
			bytes.WriteByte( value );
		}
		throw new InvalidDataException( "Unity string exceeds the supported length." );
	}

	public string ReadAlignedString()
	{
		var size = ReadInt32();
		if ( size < 0 || size > 4096 ) throw new InvalidDataException( "Invalid Unity string length." );
		var value = Encoding.UTF8.GetString( ReadExactBytes( size ) );
		Align( 4 );
		return value;
	}
}

using System.Numerics;

namespace UnityMount.Wwise;

ref struct VorbisBits
{
	readonly ReadOnlySpan<byte> _data;
	public int Position { get; private set; }

	public VorbisBits( ReadOnlySpan<byte> data ) => _data = data;
	public readonly int Remaining => _data.Length * 8 - Position;

	public uint Read( int width )
	{
		if ( width < 0 || width > 32 || width > Remaining ) throw new InvalidDataException( "Truncated Vorbis bitstream." );
		uint value = 0;
		for ( var i = 0; i < width; i++, Position++ )
			value |= (uint)((_data[Position / 8] >> (Position % 8)) & 1) << i;
		return value;
	}

	public uint Copy( VorbisPacket output, int width )
	{
		var value = Read( width );
		output.Write( value, width );
		return value;
	}

	public static int Width( uint value ) => 32 - BitOperations.LeadingZeroCount( value );
}

sealed class VorbisPacket
{
	readonly List<byte> _bytes = new();
	int _bits;

	public VorbisPacket() { }

	public VorbisPacket( byte header )
	{
		Write( header, 8 );
		foreach ( var value in "vorbis" ) Write( value, 8 );
	}

	public void Write( uint value, int width )
	{
		for ( var i = 0; i < width; i++, _bits++ )
		{
			if ( (_bits & 7) == 0 ) _bytes.Add( 0 );
			if ( (value & (1u << i)) != 0 ) _bytes[^1] |= (byte)(1 << (_bits & 7));
		}
	}

	public byte[] Finish() => _bytes.ToArray();
}

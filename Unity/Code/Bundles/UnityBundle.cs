using Sandbox.Compression;

namespace UnityMount;

sealed class UnityBundle
{
	const int CompressionMask = 0x3f;
	const int DirectoryAtEnd = 0x80;
	const int AlignBlockData = 0x200;
	const int MaxDirectorySize = 16 * 1024 * 1024;
	const int MaxBlockSize = 64 * 1024 * 1024;

	public readonly record struct Entry( string Name, long Offset, long Length, bool Serialized, string ExternalPath = null );
	readonly record struct Block( long Offset, int Size, long FileOffset, int StoredSize, int Compression );
	readonly Block[] _blocks;
	readonly bool _standalone;
	readonly UnityBundleBlockCache _blockCache;

	public string Path { get; }
	public Entry[] Entries { get; }

	UnityBundle( string path, Block[] blocks, Entry[] entries, bool standalone = false, UnityBundleBlockCache blockCache = null )
	{
		Path = path;
		_blocks = blocks;
		_standalone = standalone;
		_blockCache = blockCache;
		Entries = entries;
	}

	public static UnityBundle OpenSerializedFile( string path, long length, Entry? sidecar = null )
	{
		var entry = new Entry( System.IO.Path.GetFileName( path ), 0, length, true );
		return new( path, [], sidecar is { } resource ? [entry, resource] : [entry], standalone: true );
	}

	public static UnityBundle Read( Stream stream, string path, UnityBundleBlockCache blockCache = null )
	{
		using var reader = new UnityReader( stream );
		if ( reader.ReadNullTerminatedString() != "UnityFS" )
			throw new InvalidDataException( "Not a UnityFS bundle." );

		var version = reader.ReadBigInt32();
		if ( version is < 6 or > 8 ) throw new NotSupportedException( $"UnityFS version {version}." );
		reader.ReadNullTerminatedString(); // Minimum player version.
		reader.ReadNullTerminatedString(); // Build version.
		var fileSize = reader.ReadBigInt64();
		var storedDirectorySize = reader.ReadBigInt32();
		var directorySize = reader.ReadBigInt32();
		var flags = reader.ReadBigInt32();
		if ( fileSize != stream.Length || directorySize is <= 0 or > MaxDirectorySize ||
			storedDirectorySize is <= 0 or > MaxDirectorySize )
			throw new InvalidDataException( "Invalid UnityFS directory size." );

		if ( version >= 7 ) reader.Align( 16 );
		var dataStart = reader.Position;
		var dataEnd = fileSize;
		if ( (flags & DirectoryAtEnd) != 0 )
		{
			dataEnd -= storedDirectorySize;
			if ( dataEnd < dataStart ) throw new InvalidDataException( "Invalid UnityFS directory offset." );
			reader.Position = dataEnd;
		}

		var directoryBytes = Decode( reader.ReadExactBytes( storedDirectorySize ), directorySize, flags );
		if ( (flags & DirectoryAtEnd) == 0 ) dataStart = reader.Position;
		if ( (flags & AlignBlockData) != 0 ) dataStart = (dataStart + 15) & ~15L;

		using var directoryStream = new MemoryStream( directoryBytes, writable: false );
		using var directory = new UnityReader( directoryStream );
		directory.Skip( 16 ); // Uncompressed data hash.
		var blockCount = directory.ReadBigInt32();
		if ( blockCount < 0 || blockCount > directory.Remaining / 10 )
			throw new InvalidDataException( "Invalid UnityFS block count." );

		var blocks = new Block[blockCount];
		long offset = 0;
		var fileOffset = dataStart;
		for ( var i = 0; i < blockCount; i++ )
		{
			var size = directory.ReadBigInt32();
			var storedSize = directory.ReadBigInt32();
			var compression = directory.ReadBigUInt16();
			if ( size is <= 0 or > MaxBlockSize || storedSize is <= 0 or > MaxBlockSize || storedSize > dataEnd - fileOffset )
				throw new InvalidDataException( "Invalid UnityFS block size." );
			blocks[i] = new( offset, size, fileOffset, storedSize, compression );
			offset += size;
			fileOffset += storedSize;
		}

		var entryCount = directory.ReadBigInt32();
		if ( entryCount < 0 || entryCount > directory.Remaining / 21 )
			throw new InvalidDataException( "Invalid UnityFS entry count." );

		var entries = new Entry[entryCount];
		for ( var i = 0; i < entryCount; i++ )
		{
			var entryOffset = directory.ReadBigInt64();
			var length = directory.ReadBigInt64();
			var entryFlags = directory.ReadBigInt32();
			var name = directory.ReadNullTerminatedString();
			if ( entryOffset < 0 || length < 0 || entryOffset > offset || length > offset - entryOffset )
				throw new InvalidDataException( $"Invalid UnityFS entry range for '{name}'." );
			entries[i] = new( name, entryOffset, length, (entryFlags & 4) != 0 );
		}

		return new( path, blocks, entries, blockCache: blockCache );
	}

	public byte[] ReadRange( BaseFileSystem fileSystem, Entry entry, long offset, int length )
	{
		using var stream = fileSystem.OpenRead( entry.ExternalPath ?? Path );
		return ReadRange( stream, entry, offset, length );
	}

	public byte[] ReadRange( Stream stream, Entry entry, long offset, int length )
	{
		if ( offset < 0 || length < 0 || offset > entry.Length || length > entry.Length - offset )
			throw new InvalidDataException( $"Invalid read range in '{entry.Name}'." );
		var result = new byte[length];
		if ( length == 0 ) return result;

		var start = entry.Offset + offset;
		if ( _standalone )
		{
			stream.Position = start;
			stream.ReadExactly( result );
			return result;
		}

		var end = start + length;
		using var reader = new UnityReader( stream );
		for ( var index = FindBlock( start ); index < _blocks.Length; index++ )
		{
			var block = _blocks[index];
			if ( block.Offset >= end ) break;

			var copyStart = Math.Max( block.Offset, start );
			var count = (int)(Math.Min( block.Offset + block.Size, end ) - copyStart);
			var blockOffset = (int)(copyStart - block.Offset);
			var destination = result.AsSpan( (int)(copyStart - start), count );
			if ( (block.Compression & CompressionMask) == 0 )
			{
				if ( block.StoredSize != block.Size ) throw new InvalidDataException( "Incorrect UnityFS uncompressed size." );
				stream.Position = block.FileOffset + blockOffset;
				stream.ReadExactly( destination );
				continue;
			}

			if ( _blockCache?.Find( this, index ) is not { } decoded )
			{
				reader.Position = block.FileOffset;
				decoded = Decode( reader.ReadExactBytes( block.StoredSize ), block.Size, block.Compression );
				_blockCache?.Add( this, index, decoded );
			}
			decoded.AsSpan( blockOffset, count ).CopyTo( destination );
		}
		return result;
	}

	int FindBlock( long offset )
	{
		var low = 0;
		var high = _blocks.Length;
		while ( low < high )
		{
			var mid = low + (high - low) / 2;
			var block = _blocks[mid];
			if ( block.Offset + block.Size <= offset ) low = mid + 1;
			else high = mid;
		}
		return low;
	}

	static byte[] Decode( byte[] stored, int size, int flags )
	{
		switch ( flags & CompressionMask )
		{
			case 0:
				if ( stored.Length != size ) throw new InvalidDataException( "Incorrect UnityFS uncompressed size." );
				return stored;
			case 2:
			case 3:
				var output = new byte[size];
				if ( LZ4.DecompressBlock( stored, output ) != size )
					throw new InvalidDataException( "Incorrect UnityFS LZ4 output size." );
				return output;
			default:
				throw new NotSupportedException( $"UnityFS compression {flags & CompressionMask}." );
		}
	}
}

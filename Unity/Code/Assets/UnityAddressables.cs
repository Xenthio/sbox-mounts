using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Serialization;

namespace UnityMount;

sealed class UnityAddressables( Dictionary<string, string[]> paths )
{
	sealed class Catalog
	{
		[JsonPropertyName( "m_InternalIds" )]
		public string[] InternalIds { get; set; }
		[JsonPropertyName( "m_KeyDataString" )]
		public string KeyData { get; set; }
		[JsonPropertyName( "m_BucketDataString" )]
		public string BucketData { get; set; }
		[JsonPropertyName( "m_EntryDataString" )]
		public string EntryData { get; set; }
	}

	public IEnumerable<string> GetPaths( string guid ) => paths.GetValueOrDefault( guid ) ?? [];

	public static UnityAddressables Read( BaseFileSystem fileSystem, string path )
	{
		using var stream = fileSystem.OpenRead( path );
		if ( stream.Length > 64 * 1024 * 1024 ) throw new InvalidDataException( "Unity Addressables catalog is too large." );
		using var json = new StreamReader( stream );
		var catalog = Json.Deserialize<Catalog>( json.ReadToEnd() );
		if ( catalog?.InternalIds is null || catalog.KeyData is null ||
			catalog.BucketData is null || catalog.EntryData is null )
			throw new InvalidDataException( "Incomplete Unity Addressables catalog." );

		var keys = Convert.FromBase64String( catalog.KeyData );
		var entries = Convert.FromBase64String( catalog.EntryData );
		using var entryReader = new UnityReader( new MemoryStream( entries ) );
		var entryCount = entryReader.ReadInt32();
		const int entrySize = 7 * sizeof( int );
		if ( entryCount < 0 || (long)entryCount * entrySize != entryReader.Remaining )
			throw new InvalidDataException( "Invalid Unity Addressables entries." );

		var paths = new Dictionary<string, string[]>( StringComparer.OrdinalIgnoreCase );
		using var buckets = new UnityReader( new MemoryStream( Convert.FromBase64String( catalog.BucketData ) ) );
		var bucketCount = buckets.ReadInt32();
		if ( bucketCount < 0 || bucketCount > buckets.Remaining / 8 )
			throw new InvalidDataException( "Invalid Unity Addressables buckets." );

		for ( var i = 0; i < bucketCount; i++ )
		{
			var offset = buckets.ReadInt32();
			var count = buckets.ReadInt32();
			if ( count < 0 || count > buckets.Remaining / 4 )
				throw new InvalidDataException( "Invalid Unity Addressables bucket size." );

			// Type 0 is an ASCII string. We only need GUID keys.
			if ( offset < 4 || offset > keys.Length - 37 || keys[offset] != 0 ||
				BinaryPrimitives.ReadInt32LittleEndian( keys.AsSpan( offset + 1, 4 ) ) != 32 )
			{
				buckets.Skip( (long)count * 4 );
				continue;
			}
			var guid = Encoding.ASCII.GetString( keys, offset + 5, 32 );
			if ( !guid.All( char.IsAsciiHexDigit ) )
			{
				buckets.Skip( (long)count * 4 );
				continue;
			}

			var locations = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
			for ( var j = 0; j < count; j++ )
			{
				var index = buckets.ReadInt32();
				if ( index < 0 || index >= entryCount ) throw new InvalidDataException( "Invalid Unity Addressables entry index." );
				entryReader.Position = 4L + (long)index * entrySize;
				var internalId = entryReader.ReadInt32();
				if ( internalId < 0 || internalId >= catalog.InternalIds.Length ) throw new InvalidDataException( "Invalid Unity Addressables location." );
				locations.Add( catalog.InternalIds[internalId] );
			}
			paths.TryAdd( guid, locations.ToArray() );
		}
		return new( paths );
	}
}

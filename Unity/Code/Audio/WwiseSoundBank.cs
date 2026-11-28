namespace UnityMount.Wwise;

static class WwiseSoundBank
{
	readonly record struct Sample( uint Id, long Offset, int Size );

	readonly record struct MediaName( string Name, string ExternalFile );

	public static Dictionary<uint, string> Mount( MountContext context, BaseFileSystem fileSystem, string path,
		Func<string, bool> select = null, IReadOnlySet<uint> eventMedia = null, IReadOnlySet<uint> loopMedia = null )
	{
		var registered = new Dictionary<uint, string>();
		if ( !fileSystem.FileExists( path ) ) return registered;

		var bank = Path.GetFileNameWithoutExtension( path );
		var directory = path[..(path.LastIndexOf( '/' ) + 1)];
		var names = ReadNames( fileSystem, Path.ChangeExtension( path, ".txt" ) );
		var used = new HashSet<string>( StringComparer.OrdinalIgnoreCase );


		foreach ( var (id, media) in names )
		{
			if ( media.ExternalFile is null || !Include( id, media.Name ) ) continue;
			var file = $"{directory}Media/{media.ExternalFile}";
			if ( !fileSystem.FileExists( file ) ) file = $"{directory}{media.ExternalFile}";
			if ( !fileSystem.FileExists( file ) ) continue;

			var name = ReserveName( media.Name, id, used );
			if ( name is null ) continue;
			Register( context, id, name, new WwiseSoundLoader( file, loop: Loops( id ) ) );
		}

		using var stream = fileSystem.OpenRead( path );
		using var reader = new UnityReader( stream );
		foreach ( var sample in ReadIndex( reader ) )
		{
			if ( registered.ContainsKey( sample.Id ) ) continue;
			var authoredName = names.GetValueOrDefault( sample.Id ).Name;
			if ( !Include( sample.Id, authoredName ) ) continue;

			// Streamed sounds can have an incomplete prefetch copy in DIDX.
			reader.Position = sample.Offset;
			if ( reader.ReadUInt32() != 0x46464952 || reader.ReadUInt32() != sample.Size - 8 ) continue;

			var name = ReserveName( authoredName, sample.Id, used );
			if ( name is null ) continue;
			Register( context, sample.Id, name, new WwiseSoundLoader( path, sample.Offset, sample.Size, Loops( sample.Id ) ) );
		}
		return registered;

		bool Include( uint id, string name ) => select is null || eventMedia?.Contains( id ) == true || name is not null && select( name );
		bool Loops( uint id ) => loopMedia?.Contains( id ) == true;

		void Register( MountContext mount, uint id, string name, WwiseSoundLoader loader )
		{
			var resource = $"sounds/wwise/{bank}/{name}.vsnd";
			mount.Add( ResourceType.Sound, resource, loader );
			registered.Add( id, resource );
		}
	}

	static string ReserveName( string name, uint id, HashSet<string> used )
	{
		if ( string.IsNullOrWhiteSpace( name ) ||
			!name.All( character => char.IsAsciiLetterOrDigit( character ) || character is '_' or '-' or '.' or ' ' ) )
			name = $"{id}";
		if ( used.Add( name ) ) return name;
		name = $"{name}_{id}";
		return used.Add( name ) ? name : null;
	}

	static Sample[] ReadIndex( UnityReader reader )
	{
		if ( reader.ReadUInt32() != 0x44484b42 ) throw new InvalidDataException( "Invalid Wwise bank header." ); // BKHD
		reader.Position = 0;
		var index = (Offset: 0L, Size: 0);
		var data = (Offset: 0L, Size: 0);
		while ( reader.Remaining >= 8 )
		{
			var tag = reader.ReadUInt32();
			var size = reader.ReadInt32();
			if ( size < 0 || size > reader.Remaining ) throw new InvalidDataException( "Invalid Wwise bank chunk size." );
			if ( tag == 0x58444944 ) index = (reader.Position, size); // DIDX
			if ( tag == 0x41544144 ) data = (reader.Position, size); // DATA
			if ( index.Size > 0 && data.Size > 0 ) break;
			reader.Skip( size );
		}
		if ( index.Size == 0 || data.Size == 0 ) return [];
		if ( index.Size % 12 != 0 || index.Size > 4 * 1024 * 1024 ) throw new InvalidDataException( "Invalid Wwise bank media index." );

		var media = new List<Sample>();
		reader.Position = index.Offset;
		for ( var i = 0; i < index.Size / 12; i++ )
		{
			var id = reader.ReadUInt32();
			var offset = reader.ReadUInt32();
			var size = reader.ReadInt32();
			if ( offset > data.Size || size < 24 || size > WwiseVorbis.MaxEncodedBytes || size > data.Size - offset ) continue;
			media.Add( new( id, data.Offset + offset, size ) );
		}
		return media.ToArray();
	}

	static Dictionary<uint, MediaName> ReadNames( BaseFileSystem fileSystem, string path )
	{
		var result = new Dictionary<uint, MediaName>();
		if ( !fileSystem.FileExists( path ) ) return result;
		using var stream = fileSystem.OpenRead( path );
		using var reader = new StreamReader( stream );
		while ( reader.ReadLine() is { } line )
		{
			var streamed = line.StartsWith( "Streamed Audio\t", StringComparison.Ordinal );
			if ( !streamed && !line.StartsWith( "In Memory Audio\t", StringComparison.Ordinal ) ) continue;
			while ( reader.ReadLine() is { Length: > 0 } row && row[0] == '\t' )
			{
				var fields = row.Split( '\t' );
				if ( fields.Length < 3 || !uint.TryParse( fields[1], out var id ) ) continue;
				if ( !streamed )
				{
					result.TryAdd( id, new( fields[2], null ) );
					continue;
				}

				// The generated name is the numeric media ID. Ignore paths to authoring machines.
				if ( fields.Length >= 5 && fields[4].Equals( $"{id}.wem", StringComparison.OrdinalIgnoreCase ) )
					result[id] = new( fields[2], fields[4] );
			}
		}
		return result;
	}
}

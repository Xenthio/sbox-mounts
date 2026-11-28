namespace UnityMount;

readonly record struct UnityReference( int FileId, long Id )
{
	public bool IsLocal => FileId == 0 && Id != 0;
	public static UnityReference Read( UnityReader reader ) => new( reader.ReadInt32(), reader.ReadInt64() );

	public static UnityReference[] ReadArray( UnityReader reader )
	{
		var count = reader.ReadInt32();
		if ( count < 0 || count > reader.Remaining / 12 ) throw new InvalidDataException( "Invalid Unity reference count." );
		var references = new UnityReference[count];
		for ( var i = 0; i < count; i++ ) references[i] = Read( reader );
		return references;
	}
}

namespace UnityMount;

/// <summary>Uses a matching embedded schema when a serialized file omits its type tree.</summary>
sealed class UnityTypeSchemas
{
	readonly System.Collections.Concurrent.ConcurrentDictionary<(int ClassId, Guid Hash), UnityTypeTree> _trees = new();

	public UnityTypeTree Find( int classId, Guid hash ) => _trees.GetValueOrDefault( (classId, hash) );

	public void Add( int classId, Guid hash, UnityTypeTree tree )
	{
		if ( hash != Guid.Empty && tree is not null ) _trees.TryAdd( (classId, hash), tree );
	}
}

namespace UnityMount;

sealed class UnityBundleBlockCache( int capacity = 32 * 1024 * 1024 )
{
	readonly record struct BlockKey( UnityBundle Bundle, int Index );
	readonly record struct Entry( BlockKey Key, byte[] Data );

	readonly object _lock = new();
	readonly LinkedList<Entry> _recent = new();
	readonly Dictionary<BlockKey, LinkedListNode<Entry>> _blocks = new();
	int _bytes;

	public byte[] Find( UnityBundle bundle, int index )
	{
		lock ( _lock )
		{
			if ( !_blocks.TryGetValue( new( bundle, index ), out var node ) ) return null;
			_recent.Remove( node );
			_recent.AddFirst( node );
			return node.Value.Data;
		}
	}

	public void Add( UnityBundle bundle, int index, byte[] data )
	{
		if ( data.Length > capacity ) return;

		var key = new BlockKey( bundle, index );
		lock ( _lock )
		{
			// Another reader may have decoded the same block while this read was in flight.
			if ( _blocks.ContainsKey( key ) ) return;

			while ( data.Length > capacity - _bytes )
			{
				var oldest = _recent.Last.Value;
				_recent.RemoveLast();
				_blocks.Remove( oldest.Key );
				_bytes -= oldest.Data.Length;
			}

			_blocks.Add( key, _recent.AddFirst( new Entry( key, data ) ) );
			_bytes += data.Length;
		}
	}
}

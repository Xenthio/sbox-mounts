using System.Text;

namespace UnityMount;

/// <summary>Field layout embedded in a Unity serialized file.</summary>
sealed class UnityTypeTree( string type, string name, int size, bool align, UnityTypeTree[] children )
{
	readonly int _size = size;
	readonly bool _align = align;
	// Fixed-size compound fields can still align a child relative to the stream.
	readonly bool _canSkip = size >= 0 && children.All( child => child._canSkip && !child._align );

	public string Name { get; } = name;
	public UnityTypeTree[] Children { get; } = children;

	readonly record struct Node( byte Depth, uint Type, uint Name, int Size, bool Align );

	public static UnityTypeTree Read( UnityReader reader, bool keep )
	{
		var count = reader.ReadInt32();
		var stringSize = reader.ReadInt32();
		if ( count < 0 || stringSize < 0 || 32L * count + stringSize > reader.Remaining )
			throw new InvalidDataException( "Invalid Unity type tree size." );
		if ( !keep || count == 0 )
		{
			reader.Skip( 32L * count + stringSize );
			return null;
		}

		var nodes = new Node[count];
		for ( var i = 0; i < count; i++ )
		{
			reader.Skip( 2 ); // Node version.
			var depth = reader.ReadByte();
			reader.Skip( 1 ); // Type flags.
			var type = reader.ReadUInt32();
			var name = reader.ReadUInt32();
			var size = reader.ReadInt32();
			reader.Skip( 4 ); // Field index.
			var flags = reader.ReadInt32();
			reader.Skip( 8 ); // Referenced type hash.
			nodes[i] = new( depth, type, name, size, (flags & 0x4000) != 0 );
		}

		var strings = reader.ReadExactBytes( stringSize );
		var index = 0;
		var tree = Build( 0 );
		if ( index != count ) throw new InvalidDataException( "Invalid Unity type tree hierarchy." );
		return tree;

		UnityTypeTree Build( byte depth )
		{
			var node = nodes[index++];
			if ( node.Depth != depth || depth > 64 ) throw new InvalidDataException( "Invalid Unity type tree depth." );

			var children = new List<UnityTypeTree>();
			while ( index < count && nodes[index].Depth > depth )
				children.Add( Build( (byte)(depth + 1) ) );
			return new( Resolve( node.Type ), Resolve( node.Name ), node.Size, node.Align, children.ToArray() );
		}

		string Resolve( uint offset )
		{
			if ( (offset & 0x80000000) != 0 )
			{
				// Only the common strings used to identify fields and variable-length layouts are needed.
				return (offset & 0x7fffffff) switch
				{
					49 => "Array",
					106 => "data",
					299 => "m_Curve",
					349 => "m_Enabled",
					374 => "m_GameObject",
					387 => "m_Index",
					427 => "m_Name",
					490 => "m_Script",
					519 => "m_Type",
					840 => "string",
					894 => "TypelessData",
					_ => ""
				};
			}

			var end = offset < strings.Length ? Array.IndexOf( strings, (byte)0, (int)offset ) : -1;
			if ( end < 0 ) throw new InvalidDataException( "Invalid Unity type tree string." );
			return Encoding.UTF8.GetString( strings, (int)offset, end - (int)offset );
		}
	}

	public bool HasField( string name ) => Children.Any( child => child.Name == name );

	public void VisitArray( UnityReader reader, Action<UnityTypeTree, long> visit )
	{
		if ( Children.Length != 1 || Children[0].Name != "Array" || Children[0].Children.Length != 2 )
			throw new InvalidDataException( $"Invalid Unity array layout for '{Name}'." );

		var count = reader.ReadInt32();
		if ( count < 0 || count > reader.Remaining || count > 16384 )
			throw new InvalidDataException( $"Invalid Unity array count for '{Name}'." );
		var item = Children[0].Children[1];
		for ( var i = 0; i < count; i++ )
		{
			var position = reader.Position;
			visit( item, position );
			reader.Position = position;
			item.SkipValue( reader );
		}
	}

	public UnityTypeTree SeekField( UnityReader reader, string name )
	{
		foreach ( var child in Children )
		{
			if ( child.Name == name ) return child;
			child.SkipValue( reader );
		}
		throw new InvalidDataException( $"Missing Unity field '{name}'." );
	}

	void SkipValue( UnityReader reader )
	{
		if ( _canSkip )
			reader.Skip( _size );
		else if ( type is "string" or "TypelessData" )
			reader.Skip( reader.ReadInt32() );
		else if ( type == "Array" && Children.Length == 2 )
		{
			var count = reader.ReadInt32();
			var item = Children[1];
			if ( count < 0 || count > reader.Remaining ) throw new InvalidDataException( $"Invalid Unity array count at {reader.Position - 4}." );
			if ( item._canSkip && !item._align )
				reader.Skip( (long)count * item._size );
			else
				for ( var i = 0; i < count; i++ ) item.SkipValue( reader );
		}
		else
		{
			foreach ( var child in Children ) child.SkipValue( reader );
		}

		if ( _align || type == "string" ) reader.Align( 4 );
	}
}

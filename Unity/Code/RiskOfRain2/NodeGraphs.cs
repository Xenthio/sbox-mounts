namespace UnityMount;

static class RiskOfRain2NodeGraphs
{
	public static RiskOfRain2NodeGraph Read( UnityAssetFile file, SerializedFile.Object asset )
	{
		var tree = asset.Tree ?? throw new InvalidDataException( "Missing Risk of Rain 2 node graph layout." );
		const int maxGraphSize = 64 * 1024 * 1024;
		if ( asset.Size > maxGraphSize ) throw new InvalidDataException( "Risk of Rain 2 node graph exceeds the supported size." );
		using var reader = new UnityReader( file.ReadObject( asset, maxGraphSize ), leaveOpen: false );

		Seek( "m_Name" );
		var name = reader.ReadAlignedString();
		Seek( "nodes" );
		var nodes = new RiskOfRain2NodeGraph.Node[ReadCount( 32 )];
		for ( var i = 0; i < nodes.Length; ++i )
		{
			var position = UnityCoordinates.Position( ReadVector() );
			var firstLink = reader.ReadInt32();
			var linkCount = reader.ReadUInt32();
			var forbiddenHulls = reader.ReadInt32();
			// The serialized line-of-sight mask is empty. Unity aligns each following byte to four bytes.
			var gateIndex = ReadAlignedByte();
			nodes[i] = new( position, firstLink, linkCount, forbiddenHulls, gateIndex, ReadAlignedByte() );
		}

		Seek( "links" );
		var links = new RiskOfRain2NodeGraph.Link[ReadCount( 32 )];
		for ( var i = 0; i < links.Length; ++i )
		{
			links[i] = new( reader.ReadInt32(), reader.ReadInt32(), reader.ReadSingle(), reader.ReadSingle(),
				UnityCoordinates.ToInches( reader.ReadSingle() ), reader.ReadInt32(), reader.ReadInt32(), ReadAlignedByte() );
		}

		Seek( "gateNames" );
		var gateNames = new string[ReadCount( 4 )];
		for ( var i = 0; i < gateNames.Length; ++i )
			gateNames[i] = reader.ReadAlignedString();

		Seek( "cellSize" );
		return new()
		{
			GraphName = name,
			CellSize = UnityCoordinates.ToInches( UnityCoordinates.Scale( ReadVector() ) ),
			Nodes = nodes,
			Links = links,
			GateNames = gateNames
		};

		void Seek( string field )
		{
			reader.Position = 0;
			tree.SeekField( reader, field );
		}

		int ReadCount( int minimumElementSize )
		{
			var count = reader.ReadInt32();
			if ( count < 0 || count > reader.Remaining / minimumElementSize )
				throw new InvalidDataException( "Invalid Risk of Rain 2 node graph array size." );
			return count;
		}

		byte ReadAlignedByte()
		{
			var value = reader.ReadByte();
			reader.Align( 4 );
			return value;
		}

		System.Numerics.Vector3 ReadVector() => new( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
	}
}

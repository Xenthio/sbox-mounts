namespace UnityMount;

sealed class UnityAvatar( int[] parents, uint[] ids, Transform[] bindPose, Dictionary<uint, string> paths )
{
	public const int ClassId = 90;
	public const int AnimatorClassId = 95;

	public static UnityAvatar Read( UnityReader reader, UnityTypeTree tree )
	{
		var avatar = tree.SeekField( reader, "m_Avatar" );
		var avatarStart = reader.Position;
		var skeleton = avatar.SeekField( reader, "m_AvatarSkeleton" ).SeekField( reader, "data" );
		var skeletonStart = reader.Position;
		var parents = new List<int>();
		skeleton.SeekField( reader, "m_Node" ).VisitArray( reader, (node, _) =>
		{
			node.SeekField( reader, "m_ParentId" );
			var parent = reader.ReadInt32();
			if ( parent < -1 || parent >= parents.Count ) throw new InvalidDataException( "Invalid Unity avatar hierarchy." );
			parents.Add( parent );
		} );
		reader.Position = skeletonStart;
		skeleton.SeekField( reader, "m_ID" );
		var ids = ReadHashes( reader, parents.Count );

		reader.Position = avatarStart;
		var pose = avatar.SeekField( reader, "m_AvatarSkeletonPose" ).SeekField( reader, "data" );
		var bindPose = new List<Transform>();
		pose.SeekField( reader, "m_X" ).VisitArray( reader, (item, start) =>
		{
			item.SeekField( reader, "t" );
			var translation = ReadVector( reader );
			reader.Position = start;
			item.SeekField( reader, "q" );
			var rotation = new System.Numerics.Quaternion( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
			reader.Position = start;
			item.SeekField( reader, "s" );
			var scale = ReadVector( reader );
			if ( !float.IsFinite( translation.LengthSquared() ) || !float.IsFinite( rotation.LengthSquared() ) ||
				rotation.LengthSquared() == 0 || !float.IsFinite( scale.LengthSquared() ) )
				throw new InvalidDataException( "Invalid Unity avatar pose." );
			bindPose.Add( new( UnityCoordinates.Position( translation ), UnityCoordinates.RotationFor( rotation ), UnityCoordinates.Scale( scale ) ) );
		} );
		if ( ids.Length != parents.Count || bindPose.Count != parents.Count )
			throw new InvalidDataException( "Unity avatar skeleton and poses have different lengths." );

		return new( parents.ToArray(), ids, bindPose.ToArray(), ReadPaths( reader, tree ) );
	}

	public static Dictionary<uint, uint> ReadPathAliases( UnityReader reader, UnityTypeTree tree )
	{
		var paths = ReadPaths( reader, tree );
		var aliases = new Dictionary<uint, uint>();
		var ambiguous = new HashSet<uint>();
		foreach ( var (hash, path) in paths )
		{
			var separator = path.IndexOf( '/' );
			if ( separator < 0 ) continue;
			var relative = UnitySkin.HashPath( path[(separator + 1)..] );
			// Retain exact matches and reject aliases shared by multiple authored paths.
			if ( !paths.ContainsKey( relative ) && !aliases.TryAdd( relative, hash ) ) ambiguous.Add( relative );
		}
		foreach ( var hash in ambiguous ) aliases.Remove( hash );
		return aliases;
	}

	static Dictionary<uint, string> ReadPaths( UnityReader reader, UnityTypeTree tree )
	{
		reader.Position = 0;
		var paths = new Dictionary<uint, string>();
		tree.SeekField( reader, "m_TOS" ).VisitArray( reader, (_, _) => paths.TryAdd( reader.ReadUInt32(), reader.ReadAlignedString() ) );
		return paths;
	}

	public UnitySkin.Joint[] ResolveJoints( uint[] meshHashes )
	{
		if ( meshHashes.Length == 0 ) return null;
		var slots = new Dictionary<int, int>();
		var indices = new int[meshHashes.Length];
		var meshPaths = new string[meshHashes.Length];
		for ( var i = 0; i < meshHashes.Length; i++ )
		{
			var index = Array.IndexOf( ids, meshHashes[i] );
			if ( index < 0 || !slots.TryAdd( index, i ) || !paths.TryGetValue( meshHashes[i], out meshPaths[i] ) ) return null;
			indices[i] = index;
		}

		var names = UnitySkin.MakeBoneNames( meshPaths.Select( path => path[(path.LastIndexOf( '/' ) + 1)..] ).ToArray() );
		if ( names is null ) return null;
		var joints = new UnitySkin.Joint[meshHashes.Length];
		for ( var i = 0; i < joints.Length; i++ )
		{
			var index = indices[i];
			var parent = parents[index];
			while ( parent >= 0 && !slots.ContainsKey( parent ) ) parent = parents[parent];
			var hashes = new List<uint> { meshHashes[i] };
			var path = meshPaths[i];
			for ( var slash = path.IndexOf( '/' ); slash >= 0; slash = path.IndexOf( '/', slash + 1 ) )
				hashes.Add( UnitySkin.HashPath( path[(slash + 1)..] ) );
			joints[i] = new( names[i], parent < 0 ? -1 : slots[parent], parent >= 0 && parents[index] == parent,
				hashes.ToArray(), bindPose[index] );
		}
		return joints;
	}

	public static uint[] ReadHashes( UnityReader reader, int limit )
	{
		var count = reader.ReadInt32();
		if ( count < 0 || count > limit || count > reader.Remaining / sizeof( uint ) )
			throw new InvalidDataException( "Invalid Unity bone hash array." );
		var hashes = new uint[count];
		for ( var i = 0; i < count; i++ ) hashes[i] = reader.ReadUInt32();
		return hashes;
	}

	static System.Numerics.Vector3 ReadVector( UnityReader reader ) => new( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
}

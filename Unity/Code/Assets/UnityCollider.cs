namespace UnityMount;

abstract record UnityCollider( bool Enabled, bool Trigger )
{
	public const int MeshClassId = 64;
	public const int BoxClassId = 65;
	public const int SphereClassId = 135;
	public const int CapsuleClassId = 136;

	public sealed record Box( bool Enabled, bool Trigger, Vector3 Center, Vector3 Size ) : UnityCollider( Enabled, Trigger );
	public sealed record Sphere( bool Enabled, bool Trigger, Vector3 Center, float Radius ) : UnityCollider( Enabled, Trigger );
	public sealed record Capsule( bool Enabled, bool Trigger, Vector3 Start, Vector3 End, float Radius ) : UnityCollider( Enabled, Trigger );
	public sealed record Mesh( bool Enabled, bool Trigger, bool Convex, UnityReference Reference ) : UnityCollider( Enabled, Trigger );

	public static UnityCollider Read( UnityReader reader, UnityTypeTree tree, int classId )
	{
		var enabled = UnitySceneObjects.ReadComponentEnabled( reader, tree );
		var trigger = ReadBool( "m_IsTrigger" );
		if ( classId == MeshClassId )
		{
			var convex = ReadBool( "m_Convex" );
			Seek( "m_Mesh" );
			return new Mesh( enabled, trigger, convex, UnityReference.Read( reader ) );
		}

		var center = UnityCoordinates.Position( ReadVector( "m_Center" ) );
		if ( classId == BoxClassId )
		{
			var size = UnityCoordinates.ToInches( UnityCoordinates.Scale( ReadVector( "m_Size" ) ) );
			return size.x > 0 && size.y > 0 && size.z > 0 ? new Box( enabled, trigger, center, size ) : null;
		}

		Seek( "m_Radius" );
		var radius = UnityCoordinates.ToInches( reader.ReadSingle() );
		if ( radius <= 0 || !float.IsFinite( radius ) ) return null;
		if ( classId == SphereClassId ) return new Sphere( enabled, trigger, center, radius );

		Seek( "m_Height" );
		var height = UnityCoordinates.ToInches( reader.ReadSingle() );
		Seek( "m_Direction" );
		var direction = reader.ReadInt32();
		if ( !float.IsFinite( height ) || direction is < 0 or > 2 ) return null;

		// Unity's capsule height includes the hemispheres. s&box stores their centers.
		var halfSegment = height / 2 - radius;
		if ( halfSegment <= 0 ) return new Sphere( enabled, trigger, center, radius );
		var axis = direction switch
		{
			0 => Vector3.Right,
			1 => Vector3.Up,
			_ => Vector3.Forward
		};
		return new Capsule( enabled, trigger, center - axis * halfSegment, center + axis * halfSegment, radius );

		bool ReadBool( string name )
		{
			Seek( name );
			return reader.ReadBoolean();
		}

		System.Numerics.Vector3 ReadVector( string name )
		{
			Seek( name );
			var value = new System.Numerics.Vector3( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
			if ( !float.IsFinite( value.LengthSquared() ) ) throw new InvalidDataException( "Invalid Unity collider vector." );
			return value;
		}

		void Seek( string name )
		{
			reader.Position = 0;
			tree.SeekField( reader, name );
		}
	}
}

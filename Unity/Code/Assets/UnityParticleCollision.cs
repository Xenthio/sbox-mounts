namespace UnityMount;

sealed record UnityParticleCollision( uint Layers, bool IncludeDynamic, float RadiusScale, float MinimumSpeed, float MaximumSpeed,
	ParticleFloat Dampen, ParticleFloat Bounce, ParticleFloat LifetimeLoss, UnityReference[] Planes )
{
	public static UnityParticleCollision Read( UnityReader reader, UnityTypeTree tree, long start )
	{
		Field( "type" );
		var type = reader.ReadInt32();
		if ( type is not (0 or 1) ) return null;
		UnityReference[] planes = null;
		if ( type == 0 )
		{
			if ( !tree.HasField( "m_Planes" ) ) return null;
			Field( "m_Planes" );
			planes = UnityReference.ReadArray( reader ).Where( plane => plane.Id != 0 ).ToArray();
			if ( planes.Length == 0 ) return null;
		}
		Field( "collisionMode" );
		if ( reader.ReadInt32() != 0 || ReadScalar( "colliderForce" ) != 0 ) return null;
		if ( tree.HasField( "interiorCollisions" ) && ReadBool( "interiorCollisions" ) ) return null;

		Field( "collidesWith" ).SeekField( reader, "m_Bits" );
		var layers = reader.ReadUInt32();
		var radius = ReadScalar( "radiusScale" );
		var minimum = UnityCoordinates.ToInches( ReadScalar( "minKillSpeed" ) );
		var maximum = UnityCoordinates.ToInches( ReadScalar( "maxKillSpeed" ) );
		if ( (planes is null && layers == 0) || radius < 0 || minimum < 0 || maximum < minimum ||
			!float.IsFinite( minimum ) || !float.IsFinite( maximum ) ) return null;

		return new( layers, ReadBool( "collidesWithDynamic" ), radius, minimum, maximum,
			UnityEffectValues.ReadFloat( reader, Field( "m_Dampen" ) ),
			UnityEffectValues.ReadFloat( reader, Field( "m_Bounce" ) ),
			UnityEffectValues.ReadFloat( reader, Field( "m_EnergyLossOnCollision" ) ), planes );

		UnityTypeTree Field( string name )
		{
			reader.Position = start;
			return tree.SeekField( reader, name );
		}

		bool ReadBool( string name )
		{
			Field( name );
			return reader.ReadBoolean();
		}

		float ReadScalar( string name )
		{
			Field( name );
			var value = reader.ReadSingle();
			if ( !float.IsFinite( value ) ) throw new InvalidDataException( "Invalid Unity particle collision value." );
			return value;
		}
	}
}

namespace UnityMount;

sealed record UnityLine( bool Enabled, bool WorldSpace, bool Loop, bool RoundCaps, bool Lit, bool Shadows,
	Vector3[] Positions, Curve HalfWidth, Gradient Color, UnityReference[] Materials )
{
	public const int ClassId = 120;

	public static UnityLine Read( UnityReader reader, UnityTypeTree tree )
	{
		var enabled = UnitySceneObjects.ReadComponentEnabled( reader, tree );
		var worldSpace = ReadFlag( "m_UseWorldSpace" );
		var loop = ReadFlag( "m_Loop" );
		reader.Position = 0;
		tree.SeekField( reader, "m_CastShadows" );
		var shadows = reader.ReadByte() != 0;

		reader.Position = 0;
		var parameters = tree.SeekField( reader, "m_Parameters" );
		var start = reader.Position;
		// Native camera-facing lines stretch one texture along the strip and have hard corners.
		if ( ReadInt( "alignment" ) != 0 || ReadInt( "textureMode" ) != 0 || ReadInt( "numCornerVertices" ) != 0 )
			return null;
		var caps = ReadInt( "numCapVertices" ) > 0;
		reader.Position = start;
		parameters.SeekField( reader, "generateLightingData" );
		var lit = reader.ReadBoolean();
		reader.Position = start;
		parameters.SeekField( reader, "widthMultiplier" );
		var halfWidth = UnityCoordinates.ToInches( reader.ReadSingle() ) * .5f;
		if ( !float.IsFinite( halfWidth ) || halfWidth <= 0 ) return null;
		reader.Position = start;
		var width = UnityEffectValues.ReadCurve( reader, parameters.SeekField( reader, "widthCurve" ), halfWidth );
		reader.Position = start;
		var color = UnityEffectValues.ReadGradient( reader, parameters.SeekField( reader, "colorGradient" ) );

		var positions = new List<Vector3>();
		reader.Position = 0;
		tree.SeekField( reader, "m_Positions" ).VisitArray( reader, (_, _) =>
		{
			var point = UnityCoordinates.Position( new( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() ) );
			if ( !float.IsFinite( point.x ) || !float.IsFinite( point.y ) || !float.IsFinite( point.z ) )
				throw new InvalidDataException( "Invalid Unity line position." );
			positions.Add( point );
		} );
		if ( positions.Count < 2 ) return null;

		var materials = new List<UnityReference>();
		reader.Position = 0;
		tree.SeekField( reader, "m_Materials" ).VisitArray( reader, (_, _) => materials.Add( UnityReference.Read( reader ) ) );
		return new( enabled, worldSpace, loop, caps, lit, shadows, positions.ToArray(), width, color, materials.ToArray() );

		bool ReadFlag( string field )
		{
			reader.Position = 0;
			tree.SeekField( reader, field );
			return reader.ReadBoolean();
		}

		int ReadInt( string field )
		{
			reader.Position = start;
			parameters.SeekField( reader, field );
			return reader.ReadInt32();
		}
	}
}

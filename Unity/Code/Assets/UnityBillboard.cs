namespace UnityMount;

sealed record UnityBillboard( string Name, float Width, float Height, float Bottom, Vector2[] Vertices, int[] Indices,
	Vector4[] Images, UnityReference Material )
{
	public const int ClassId = 226;
	public const int RendererClassId = 227;

	public static UnityBillboard Read( UnityReader reader, UnityTypeTree tree )
	{
		tree.SeekField( reader, "m_Name" );
		var name = reader.ReadAlignedString();
		var width = UnityCoordinates.ToInches( ReadFloat( "width" ) );
		var height = UnityCoordinates.ToInches( ReadFloat( "height" ) );
		var bottom = UnityCoordinates.ToInches( ReadFloat( "bottom" ) );
		if ( !(width > 0f && height > 0f) || !float.IsFinite( width + height + MathF.Abs( bottom ) ) ) return null;

		var vertices = new List<Vector2>();
		reader.Position = 0;
		tree.SeekField( reader, "vertices" ).VisitArray( reader, (_, _) =>
			vertices.Add( new( reader.ReadSingle(), reader.ReadSingle() ) ) );

		var indices = new List<int>();
		reader.Position = 0;
		tree.SeekField( reader, "indices" ).VisitArray( reader, (_, _) => indices.Add( reader.ReadUInt16() ) );
		if ( vertices.Count < 3 || vertices.Any( point => !float.IsFinite( point.LengthSquared ) ) ||
			indices.Count == 0 || indices.Count % 3 != 0 || indices.Any( index => index >= vertices.Count ) ) return null;

		var images = new List<Vector4>();
		reader.Position = 0;
		tree.SeekField( reader, "imageTexCoords" ).VisitArray( reader, (_, _) =>
			images.Add( new( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() ) ) );
		if ( images.Count is < 1 or > 16 || images.Any( image => !float.IsFinite( image.LengthSquared ) || image.z <= 0f || image.w == 0f ) )
			return null;

		reader.Position = 0;
		tree.SeekField( reader, "material" );
		return new( name, width, height, bottom, vertices.ToArray(), indices.ToArray(), images.ToArray(), UnityReference.Read( reader ) );

		float ReadFloat( string field )
		{
			reader.Position = 0;
			tree.SeekField( reader, field );
			return reader.ReadSingle();
		}
	}
}

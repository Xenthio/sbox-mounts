namespace UnityMount;

readonly record struct UnityReflectionProbe( bool Enabled, BBox Bounds, bool BoxProjection, float Intensity,
	float BlendDistance, int Priority, UnityReference Cubemap )
{
	public const int ClassId = 215;

	public static UnityReflectionProbe? Read( UnityReader reader, UnityTypeTree tree )
	{
		tree.SeekField( reader, "m_Mode" );
		var mode = reader.ReadInt32();
		if ( mode is not (0 or 2) ) return null;

		reader.Position = 0;
		tree.SeekField( reader, mode == 0 ? "m_BakedTexture" : "m_CustomBakedTexture" );
		var cube = UnityReference.Read( reader );
		if ( cube.Id == 0 ) return null;

		var size = ReadVector( "m_BoxSize" );
		var center = ReadVector( "m_BoxOffset" );
		var bounds = BBox.FromPositionAndSize( UnityCoordinates.Position( center ),
			UnityCoordinates.ToInches( UnityCoordinates.Scale( size ) ) );
		var intensity = ReadFloat( "m_IntensityMultiplier" );
		var blend = UnityCoordinates.ToInches( ReadFloat( "m_BlendDistance" ) );
		if ( size.X <= 0 || size.Y <= 0 || size.Z <= 0 || !float.IsFinite( size.LengthSquared() ) ||
			!float.IsFinite( center.LengthSquared() ) || !float.IsFinite( intensity ) || !float.IsFinite( blend ) ) return null;

		reader.Position = 0;
		tree.SeekField( reader, "m_Importance" );
		var priority = reader.ReadInt32();
		reader.Position = 0;
		tree.SeekField( reader, "m_BoxProjection" );
		var box = reader.ReadBoolean();
		return new( UnitySceneObjects.ReadComponentEnabled( reader, tree ), bounds, box,
			Math.Max( 0, intensity ), Math.Max( 0, blend ), priority, cube );

		float ReadFloat( string field )
		{
			reader.Position = 0;
			tree.SeekField( reader, field );
			return reader.ReadSingle();
		}

		System.Numerics.Vector3 ReadVector( string field )
		{
			reader.Position = 0;
			tree.SeekField( reader, field );
			return new( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
		}
	}
}

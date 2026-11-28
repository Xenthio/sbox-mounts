namespace UnityMount;

sealed record UnityParticleShape( UnityParticleShape.Kind Type, Transform Transform, float Radius = 0,
	float Thickness = 1, float Angle = 0, float Length = 0, bool AlignToDirection = false )
{
	public enum Kind { Point = -1, Sphere = 0, Hemisphere = 2, Cone = 4, Box = 5, Mesh = 6, ConeVolume = 8, Circle = 10, Edge = 12, SkinnedMeshRenderer = 14, BoxShell = 15, Rectangle = 18 }

	public static UnityParticleShape Point { get; } = new( Kind.Point, Transform.Zero );

	public static UnityParticleShape Read( UnityReader reader, UnityTypeTree tree, long start )
	{
		reader.Position = start;
		tree.SeekField( reader, "type" );
		var type = (Kind)reader.ReadInt32();
		if ( type == Kind.SkinnedMeshRenderer )
			return new( type, Transform.Zero, AlignToDirection: ReadAlignToDirection() );
		if ( type == Kind.Mesh )
		{
			reader.Position = start;
			tree.SeekField( reader, "m_Mesh" );
			// An unassigned mesh shape has no surface to sample. Keep the emitter at its origin.
			return UnityReference.Read( reader ).Id == 0 ? Point : null;
		}
		if ( type is not (Kind.Sphere or Kind.Hemisphere or Kind.Cone or Kind.ConeVolume or Kind.Circle or
			Kind.Box or Kind.BoxShell or Kind.Edge or Kind.Rectangle) ) return null;
		var position = UnityCoordinates.Position( ReadVector( "m_Position" ) );
		var angles = ReadVector( "m_Rotation" );
		var rotation = UnityCoordinates.EulerRotation( new Vector3( angles.X, angles.Y, angles.Z ) );
		var scale = UnityCoordinates.Scale( ReadVector( "m_Scale" ) );
		if ( scale.x < 0 || scale.y < 0 || scale.z < 0 ) return null;
		var transform = new Transform( position, rotation, scale );
		if ( type is Kind.Box or Kind.BoxShell ) return new( type, transform );
		if ( type == Kind.Rectangle ) return new( type, transform, AlignToDirection: ReadAlignToDirection() );

		reader.Position = start;
		var radiusField = tree.SeekField( reader, "radius" );
		var radiusStart = reader.Position;
		radiusField.SeekField( reader, "value" );
		var radius = UnityCoordinates.ToInches( reader.ReadSingle() );
		if ( radius < 0 || !float.IsFinite( radius ) ) return null;
		if ( type == Kind.Edge )
		{
			reader.Position = radiusStart;
			radiusField.SeekField( reader, "mode" );
			if ( reader.ReadInt32() != 0 ) return null;
			reader.Position = radiusStart;
			radiusField.SeekField( reader, "spread" );
			return reader.ReadSingle() == 0 ? new( type, transform, radius, AlignToDirection: ReadAlignToDirection() ) : null;
		}
		var thickness = ReadSingle( "radiusThickness" );
		if ( thickness is < 0 or > 1 ) return null;
		if ( type is Kind.Sphere or Kind.Hemisphere ) return new( type, transform, radius, thickness );
		if ( type == Kind.Circle )
		{
			reader.Position = start;
			var arc = tree.SeekField( reader, "arc" );
			var arcStart = reader.Position;
			arc.SeekField( reader, "mode" );
			if ( reader.ReadInt32() != 0 ) return null;
			reader.Position = arcStart;
			arc.SeekField( reader, "value" );
			var sweep = reader.ReadSingle();
			if ( sweep is < 0 or > 360 || !float.IsFinite( sweep ) ) return null;
			return new( type, transform, radius, thickness, sweep, AlignToDirection: ReadAlignToDirection() );
		}
		var angle = ReadSingle( "angle" );
		var length = type == Kind.ConeVolume ? UnityCoordinates.ToInches( ReadSingle( "length" ) ) : 0;
		return angle is >= 0 and <= 90 && (angle < 90 || length == 0) && length >= 0 && float.IsFinite( length )
			? new( type, transform, radius, thickness, angle, length ) : null;

		bool ReadAlignToDirection()
		{
			reader.Position = start;
			tree.SeekField( reader, "alignToDirection" );
			return reader.ReadBoolean();
		}

		float ReadSingle( string field )
		{
			reader.Position = start;
			tree.SeekField( reader, field );
			var value = reader.ReadSingle();
			if ( !float.IsFinite( value ) ) throw new InvalidDataException( "Invalid Unity particle shape value." );
			return value;
		}

		System.Numerics.Vector3 ReadVector( string field )
		{
			reader.Position = start;
			tree.SeekField( reader, field );
			var value = new System.Numerics.Vector3( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
			if ( !float.IsFinite( value.LengthSquared() ) ) throw new InvalidDataException( "Invalid Unity particle shape vector." );
			return value;
		}
	}
}

namespace UnityMount;

readonly record struct UnitySpotCone( float Inner, float Outer );

readonly record struct UnityLightAnimationData( Curve Intensity, float Duration, bool Loop, bool RandomStart, bool Enabled );

sealed record UnityLight( bool Enabled, Color Tint, float Intensity, float Radius, bool Shadows, float Temperature, UnityLightAnimationData? Animation = null, bool Directional = false, UnitySpotCone? Spot = null )
{
	public const int ClassId = 108;

	public static UnityLight Read( UnityReader reader, UnityTypeTree tree )
	{
		tree.SeekField( reader, "m_Type" );
		var type = reader.ReadInt32();
		if ( type is not (0 or 1 or 2) ) return null;

		reader.Position = 0;
		tree.SeekField( reader, "m_Color" );
		var tint = new Color( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
		var intensity = ReadFloat( "m_Intensity" );
		var range = UnityCoordinates.ToInches( ReadFloat( "m_Range" ) );
		if ( (type != 1 && range <= 0) || intensity < 0 || !float.IsFinite( range ) || !float.IsFinite( intensity ) ) return null;

		UnitySpotCone? cone = null;
		if ( type == 0 )
		{
			// Unity stores full angles, while s&box's cone properties are half angles.
			var outer = ReadFloat( "m_SpotAngle" ) * .5f;
			if ( !(outer > 0 && outer <= 90) ) return null;
			var inner = tree.HasField( "m_InnerSpotAngle" ) ? ReadFloat( "m_InnerSpotAngle" ) * .5f
				: MathF.Atan( MathF.Tan( outer * (MathF.PI / 180) ) * (46f / 64) ) * (180 / MathF.PI);
			if ( !float.IsFinite( inner ) ) return null;
			cone = new UnitySpotCone( Math.Clamp( inner, 0, outer ), outer );
		}

		reader.Position = 0;
		var shadows = tree.SeekField( reader, "m_Shadows" );
		shadows.SeekField( reader, "m_Type" );
		var castsShadows = reader.ReadInt32() != 0;

		reader.Position = 0;
		tree.SeekField( reader, "m_UseColorTemperature" );
		var temperature = reader.ReadBoolean() ? ReadFloat( "m_ColorTemperature" ) : 6500f;
		return new( UnitySceneObjects.ReadComponentEnabled( reader, tree ), tint, intensity, range, castsShadows, temperature,
			Directional: type == 1, Spot: cone );

		float ReadFloat( string field )
		{
			reader.Position = 0;
			tree.SeekField( reader, field );
			return reader.ReadSingle();
		}
	}
}

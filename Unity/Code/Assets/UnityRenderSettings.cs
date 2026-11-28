namespace UnityMount;

readonly record struct UnityRenderSettings( UnityReference Skybox, UnitySceneFog? Fog, UnityEnvironmentReflection? EnvironmentReflection, Color? FlatAmbient )
{
	public const int ClassId = 104;

	public static UnityRenderSettings Read( UnityReader reader, UnityTypeTree tree )
	{
		tree.SeekField( reader, "m_SkyboxMaterial" );
		var skybox = UnityReference.Read( reader );
		return new( skybox, ReadFog(), ReadEnvironmentReflection(), ReadFlatAmbient() );

		Color? ReadFlatAmbient()
		{
			const int flat = 3;
			if ( !tree.HasField( "m_AmbientMode" ) || !tree.HasField( "m_AmbientSkyColor" ) ) return null;
			reader.Position = 0;
			tree.SeekField( reader, "m_AmbientMode" );
			// Flat mode uses the sky color directly. Ambient intensity belongs to the skybox mode.
			return reader.ReadInt32() == flat ? ReadColor( "m_AmbientSkyColor" ) : null;
		}

		UnitySceneFog? ReadFog()
		{
			reader.Position = 0;
			tree.SeekField( reader, "m_Fog" );
			if ( !reader.ReadBoolean() || ReadColor( "m_FogColor" ) is not { } color ) return null;
			reader.Position = 0;
			tree.SeekField( reader, "m_FogMode" );
			return reader.ReadInt32() switch
			{
				1 => ReadLinear( color ),
				2 => ReadExponential( color ),
				_ => null
			};
		}

		UnityEnvironmentReflection? ReadEnvironmentReflection()
		{
			if ( !tree.HasField( "m_ReflectionIntensity" ) || !tree.HasField( "m_DefaultReflectionMode" ) ) return null;
			var intensity = ReadFloat( "m_ReflectionIntensity" );
			if ( !float.IsFinite( intensity ) || intensity <= 0 ) return null;
			reader.Position = 0;
			tree.SeekField( reader, "m_DefaultReflectionMode" );
			var mode = reader.ReadInt32();
			var field = mode switch { 0 => "m_GeneratedSkyboxReflection", 1 => "m_CustomReflection", _ => null };
			if ( field is null || !tree.HasField( field ) ) return null;
			reader.Position = 0;
			tree.SeekField( reader, field );
			var cube = UnityReference.Read( reader );
			return cube.Id == 0 ? null : new( cube, intensity );
		}

		UnitySceneFog? ReadLinear( Color color )
		{
			var start = UnityCoordinates.ToInches( ReadFloat( "m_LinearFogStart" ) );
			var end = UnityCoordinates.ToInches( ReadFloat( "m_LinearFogEnd" ) );
			if ( !float.IsFinite( start ) || !float.IsFinite( end ) || end <= start ) return null;
			return new( color, start, end, 0 );
		}

		UnitySceneFog? ReadExponential( Color color )
		{
			var density = ReadFloat( "m_FogDensity" );
			var attenuation = UnityCoordinates.ToInches( 1 / density );
			return float.IsFinite( attenuation ) && attenuation > 0 ? new( color, 0, 0, attenuation ) : null;
		}

		float ReadFloat( string field )
		{
			reader.Position = 0;
			tree.SeekField( reader, field );
			return reader.ReadSingle();
		}

		Color? ReadColor( string field )
		{
			reader.Position = 0;
			tree.SeekField( reader, field );
			var color = new Color( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
			return float.IsFinite( color.r + color.g + color.b + color.a ) ? color : null;
		}
	}
}

readonly record struct UnitySceneFog( Color Color, float Start, float End, float AttenuationDistance );
readonly record struct UnityEnvironmentReflection( UnityReference Cubemap, float Intensity );

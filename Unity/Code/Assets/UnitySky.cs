namespace UnityMount;

sealed record UnitySky( string Name, UnityReference Cubemap, Color Tint, float Exposure, float Rotation )
{

	public static UnitySky Read( UnityReader reader, UnityTypeTree tree )
	{
		tree.SeekField( reader, "m_Name" );
		var name = reader.ReadAlignedString();
		reader.Position = 0;
		var saved = tree.SeekField( reader, "m_SavedProperties" );
		var start = reader.Position;
		saved.SeekField( reader, "m_TexEnvs" );
		var textures = reader.ReadInt32();
		if ( textures < 0 || textures > reader.Remaining / 32 )
			throw new InvalidDataException( "Invalid Unity sky texture count." );
		UnityReference cubemap = default;
		for ( var i = 0; i < textures; i++ )
		{
			var key = reader.ReadAlignedString();
			var texture = UnityReference.Read( reader );
			reader.Skip( 16 ); // Scale and offset.
			if ( key == "_Tex" ) cubemap = texture;
		}
		if ( cubemap.Id == 0 ) return null;

		reader.Position = start;
		saved.SeekField( reader, "m_Floats" );
		var floats = reader.ReadInt32();
		if ( floats < 0 || floats > reader.Remaining / 8 )
			throw new InvalidDataException( "Invalid Unity sky float count." );
		var exposure = 1f;
		var rotation = 0f;
		for ( var i = 0; i < floats; i++ )
		{
			var key = reader.ReadAlignedString();
			var value = reader.ReadSingle();
			if ( !float.IsFinite( value ) ) continue;
			if ( key == "_Exposure" ) exposure = Math.Max( 0, value );
			if ( key == "_Rotation" ) rotation = value;
		}

		reader.Position = start;
		saved.SeekField( reader, "m_Colors" );
		var colors = reader.ReadInt32();
		if ( colors < 0 || colors > reader.Remaining / 20 )
			throw new InvalidDataException( "Invalid Unity sky color count." );
		var tint = new Color( 0.5f, 0.5f, 0.5f );
		for ( var i = 0; i < colors; i++ )
		{
			var key = reader.ReadAlignedString();
			var value = new Color( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
			if ( key == "_Tint" && float.IsFinite( value.r + value.g + value.b ) ) tint = value;
		}
		return new( name, cubemap, tint, exposure, rotation );
	}

	public Color GetLinearTint()
	{
		// Unity's linear renderer converts the Gamma exposure property before applying color-space double.
		var exposure = UnityColor.ToLinear( Exposure ) * 4.5947938f;
		var tint = UnityColor.ToLinear( Tint );
		return new( tint.r * exposure, tint.g * exposure, tint.b * exposure );
	}
}

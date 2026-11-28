namespace UnityMount;

sealed record UnitySprite( string Name, UnityReference Texture, int X, int Y, int Width, int Height, Vector2 Pivot, float PixelsPerUnit )
{
	public const int ClassId = 213;

	public static UnitySprite Read( UnityReader reader, UnityTypeTree tree )
	{
		tree.SeekField( reader, "m_Name" );
		var name = reader.ReadAlignedString();

		reader.Position = 0;
		tree.SeekField( reader, "m_Rect" );
		var x = ReadCoordinate( reader );
		var y = ReadCoordinate( reader );
		var width = ReadCoordinate( reader );
		var height = ReadCoordinate( reader );
		if ( width == 0 || height == 0 || (long)width * height * 4 > UnityTexture.MaxPayloadSize )
			throw new InvalidDataException( "Invalid Unity sprite dimensions." );

		reader.Position = 0;
		tree.SeekField( reader, "m_Pivot" );
		var pivot = new Vector2( reader.ReadSingle(), reader.ReadSingle() );
		if ( !float.IsFinite( pivot.x ) || !float.IsFinite( pivot.y ) )
			throw new InvalidDataException( "Invalid Unity sprite pivot." );

		reader.Position = 0;
		tree.SeekField( reader, "m_PixelsToUnits" );
		var pixelsPerUnit = reader.ReadSingle();
		if ( !float.IsFinite( pixelsPerUnit ) || pixelsPerUnit <= 0 )
			throw new InvalidDataException( "Invalid Unity sprite scale." );

		reader.Position = 0;
		tree.SeekField( reader, "m_SpriteAtlas" );
		if ( UnityReference.Read( reader ).Id != 0 ) return null;

		reader.Position = 0;
		var renderData = tree.SeekField( reader, "m_RD" );
		var renderDataOffset = reader.Position;
		renderData.SeekField( reader, "settingsRaw" );
		if ( (reader.ReadUInt32() & 1) != 0 ) return null;

		reader.Position = renderDataOffset;
		renderData.SeekField( reader, "alphaTexture" );
		if ( UnityReference.Read( reader ).Id != 0 ) return null;

		// Unpacked render data keeps the full source image. Use the logical rect
		// to preserve the original canvas around a tighter render mesh.
		reader.Position = renderDataOffset;
		renderData.SeekField( reader, "texture" );
		return new( name, UnityReference.Read( reader ), x, y, width, height, pivot, pixelsPerUnit );
	}

	static int ReadCoordinate( UnityReader reader )
	{
		var coordinate = reader.ReadSingle();
		if ( !float.IsFinite( coordinate ) || coordinate < 0 || coordinate > 16384 )
			throw new InvalidDataException( "Invalid Unity sprite rectangle." );
		return (int)MathF.Round( coordinate );
	}
}

namespace UnityMount;

readonly record struct UnitySpriteRenderer( UnityReference Sprite, Color Tint, bool Enabled, bool FlipHorizontal, bool FlipVertical )
{
	public const int ClassId = 212;

	public static UnitySpriteRenderer? Read( UnityReader reader, UnityTypeTree tree )
	{
		var enabled = UnitySceneObjects.ReadComponentEnabled( reader, tree );
		reader.Position = 0;
		tree.SeekField( reader, "m_DrawMode" );
		if ( reader.ReadInt32() != 0 ) return null; // Sliced and tiled sprites use a different mesh.

		reader.Position = 0;
		tree.SeekField( reader, "m_Sprite" );
		var sprite = UnityReference.Read( reader );
		if ( sprite.Id == 0 ) return null;

		reader.Position = 0;
		tree.SeekField( reader, "m_Color" );
		var tint = new Color( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
		if ( !float.IsFinite( tint.r + tint.g + tint.b + tint.a ) )
			throw new InvalidDataException( "Invalid Unity sprite tint." );

		reader.Position = 0;
		tree.SeekField( reader, "m_FlipX" );
		var flipX = reader.ReadBoolean();
		reader.Position = 0;
		tree.SeekField( reader, "m_FlipY" );
		return new( sprite, tint, enabled, flipX, reader.ReadBoolean() );
	}
}

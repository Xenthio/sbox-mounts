namespace UnityMount;

readonly record struct UnityMountedSprite( string Path, float Size );

readonly record struct UnityPrefabSprite( UnityMountedSprite Sprite, UnitySpriteRenderer Source )
{
	public void AddTo( GameObject gameObject )
	{
		var sprite = GameResource.Load<Sprite>( Sprite.Path );
		if ( sprite is null ) return;

		var renderer = gameObject.AddComponent<SpriteRenderer>( false );
		renderer.Sprite = sprite;
		renderer.Billboard = SpriteRenderer.BillboardMode.None;
		renderer.Color = Source.Tint;
		// Match the Unity plane's Y and Z scale. The sprite batch takes its
		// horizontal scale from the GameObject's X axis instead.
		var scale = gameObject.WorldScale;
		var widthScale = scale.x == 0 ? 1 : MathF.Abs( scale.y / scale.x );
		renderer.Size = new( Sprite.Size * widthScale, Sprite.Size );
		// Unity's image right points towards Source's negative Y axis.
		renderer.FlipHorizontal = !Source.FlipHorizontal ^ ((scale.x < 0) != (scale.y < 0));
		renderer.FlipVertical = Source.FlipVertical;
		renderer.IsSorted = true;
		renderer.Enabled = Source.Enabled;
	}
}

sealed class UnitySpriteLoader( UnitySprite source, string texturePath ) : ResourceLoader<GameMount>
{
	const string ImagePath = "image.vtex";

	protected override ResourceLoader ResolveChild( string name, out ResourceType type )
	{
		type = ResourceType.Texture;
		return name == ImagePath ? new UnitySpriteImageLoader( source, texturePath ) : null;
	}

	protected override object Load()
	{
		var texture = Texture.Load( texturePath );
		if ( texture is null || texture.IsError ) return null;
		if ( source.X != 0 || source.Y != 0 || source.Width != texture.Width || source.Height != texture.Height )
			texture = Texture.Load( $"{Path}/{ImagePath}" );
		if ( texture is null || texture.IsError ) return null;

		var sprite = Sprite.FromTexture( texture );
		// Unity measures the pivot from the lower-left corner. Use texture UVs.
		sprite.Animations[0].Origin = new( source.Pivot.x, 1 - source.Pivot.y );
		return sprite;
	}
}

sealed class UnitySpriteImageLoader( UnitySprite source, string texturePath ) : ResourceLoader<GameMount>
{
	protected override object Load()
	{
		var texture = Texture.Load( texturePath );
		if ( texture is null || texture.IsError ||
			source.X + source.Width > texture.Width || source.Y + source.Height > texture.Height ) return null;

		var pixels = new Color32[source.Width * source.Height];
		var top = texture.Height - source.Y - source.Height;
		texture.GetPixels( (source.X, top, source.Width, source.Height), 0, 0, pixels.AsSpan(), ImageFormat.RGBA8888 );
		var format = texture.ImageFormat == ImageFormat.RGBA8888_LINEAR ? ImageFormat.RGBA8888_LINEAR : ImageFormat.RGBA8888;
		return Texture.Create( source.Width, source.Height, format )
			.WithName( Path )
			.WithData<Color32>( pixels )
			.WithStaticUsage()
			.Finish();
	}
}

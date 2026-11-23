namespace GoldSrc.HalfLife;

internal static class LaserSpot
{
	internal static SpriteRenderer Create( GameObject parent, GameMount mount, string name )
	{
		if ( mount?.GetIndexedSprite( "laserdot" ) is not { } sprite ) return null;

		var go = new GameObject( parent, true, name );
		go.NetworkMode = NetworkMode.Never;
		var dot = go.AddComponent<SpriteRenderer>();
		dot.Sprite = sprite;
		var image = sprite.Animations[0].Frames[0].Texture;
		dot.Size = new Vector2( image.Width, image.Height );
		dot.Additive = true;
		return dot;
	}
}

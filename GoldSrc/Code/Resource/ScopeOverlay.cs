using Sandbox.Rendering;

namespace GoldSrc;

internal static class ScopeOverlay
{
	internal static void Draw( Painter painter, IReadOnlyList<Texture> masks )
	{
		var bounds = painter.Bounds;
		var half = System.MathF.Min( bounds.Width, bounds.Height ) * 0.5f;
		var center = bounds.Center;
		var left = center.x - half;
		var top = center.y - half;
		var right = center.x + half;
		var bottom = center.y + half;
		using var scope = painter.Scope();
		painter.Fill = Color.Black;
		if ( left > bounds.Left ) painter.Rect( new Rect( bounds.Left, bounds.Top, left - bounds.Left, bounds.Height ) );
		if ( right < bounds.Right ) painter.Rect( new Rect( right, bounds.Top, bounds.Right - right, bounds.Height ) );
		if ( top > bounds.Top ) painter.Rect( new Rect( left, bounds.Top, half * 2, top - bounds.Top ) );
		if ( bottom < bounds.Bottom ) painter.Rect( new Rect( left, bottom, half * 2, bounds.Bottom - bottom ) );

		painter.Texture( masks[0], new Rect( left, top, half, half ), Color.White, FilterMode.Bilinear );
		painter.Texture( masks[1], new Rect( center.x, top, half, half ), Color.White, FilterMode.Bilinear );
		painter.Texture( masks[2], new Rect( left, center.y, half, half ), Color.White, FilterMode.Bilinear );
		painter.Texture( masks[3], new Rect( center.x, center.y, half, half ), Color.White, FilterMode.Bilinear );
	}
}

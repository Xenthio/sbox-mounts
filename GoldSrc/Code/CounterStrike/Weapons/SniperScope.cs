using GoldSrc.Bsp;
using Sandbox.Mounting;
using Sandbox.Rendering;

namespace GoldSrc.CounterStrike;

internal static class SniperScope
{
	static readonly string[] Masks = ["scope_arc_nw", "scope_arc_ne", "scope_arc_sw", "scope_arc"];

	internal static void Mount( GameMount mount, MountContext context )
	{
		foreach ( var mask in Masks )
		{
			if ( mount.FindFile( $"sprites/{mask}.tga" ) is { } file )
				context.Add( ResourceType.Texture, $"scope/{mask}", new ScopeMaskLoader( file ) );
		}
	}

	internal static Texture[] Load( GameMount mount ) =>
		Masks.Select( mask => Texture.Load( $"mount://{mount.Ident}/scope/{mask}.vtex" ) ).ToArray();

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

	sealed class ScopeMaskLoader( string file ) : ResourceLoader<GameMount>
	{
		protected override object Load()
		{
			if ( SkyImage.Load( Host.FileSystem, file ) is not { } mask ) return null;
			return Texture.Create( mask.Width, mask.Height, ImageFormat.RGBA8888_LINEAR )
				.WithName( Path ).WithData( mask.Rgba ).WithStaticUsage().Finish();
		}
	}
}

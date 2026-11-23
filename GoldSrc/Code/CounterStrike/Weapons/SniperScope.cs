using GoldSrc.Bsp;
using Sandbox.Mounting;

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

using System;

namespace GoldSrc.HalfLife;

/// <summary>Loads weapon selection art from the installed Half-Life HUD sprite manifests.</summary>
internal sealed class WeaponIconLoader( string className ) : ResourceLoader<GameMount>
{
	protected override object Load()
	{
		var sharedHud = Host is DayOfDefeatMount;
		var manifestName = sharedHud ? "hud" : className;
		var entryName = sharedHud ? className : "weapon";
		foreach ( var dir in Host.GameDirs )
		{
			var manifest = $"{dir}/sprites/{manifestName}.txt";
			if ( !Host.FileSystem.FileExists( manifest ) ) continue;
			var entries = Host.FileSystem.ReadAllText( manifest ).Split( '\n' )
				.Select( line => line.Split( (char[])null, StringSplitOptions.RemoveEmptyEntries ) )
				.Where( row => row.Length >= 7 && row[0] == entryName && int.TryParse( row[1], out _ ) )
				.OrderByDescending( row => int.Parse( row[1] ) );
			foreach ( var row in entries )
			{
				if ( !int.TryParse( row[3], out var x ) || !int.TryParse( row[4], out var y ) ||
					!int.TryParse( row[5], out var width ) || !int.TryParse( row[6], out var height ) ) continue;
				foreach ( var spriteDir in Host.GameDirs )
				{
					var path = $"{spriteDir}/sprites/{row[2]}.spr";
					if ( !Host.FileSystem.FileExists( path ) ) continue;
					if ( GameMount.ReadSpriteFrames( Host.FileSystem.OpenRead( path ) ) is not { Count: > 0 } frames ) continue;
					var frame = frames[0];
					if ( x < 0 || y < 0 || width <= 0 || height <= 0 || x > frame.Width - width || y > frame.Height - height ) continue;
					if ( MakeIcon( frame.Pixels, frame.Width, x, y, width, height ) is { } icon ) return icon;
				}
			}
		}
		return null;
	}

	Texture MakeIcon( byte[] pixels, int stride, int x, int y, int width, int height )
	{
		// Half-Life draws these sprites additively. Remove the dark scanline backing
		// and use the remaining intensity as coverage in the original amber HUD color.
		int left = width, right = -1, top = height, bottom = -1;
		var alpha = new byte[width * height];
		for ( int row = 0; row < height; row++ )
		for ( int col = 0; col < width; col++ )
		{
			var at = ((y + row) * stride + x + col) * 4;
			var intensity = pixels[at] * pixels[at + 3] / 255;
			if ( intensity <= 32 ) continue;
			alpha[row * width + col] = (byte)((intensity - 32) * 255 / 223);
			left = Math.Min( left, col );
			right = Math.Max( right, col );
			top = Math.Min( top, row );
			bottom = Math.Max( bottom, row );
		}
		if ( right < left ) return null;

		var artWidth = right - left + 1;
		var artHeight = bottom - top + 1;
		var canvasWidth = (int)Math.Ceiling( artWidth / 0.9f );
		var canvasHeight = (int)Math.Ceiling( artHeight / 0.9f );
		var offsetX = (canvasWidth - artWidth) / 2;
		var offsetY = (canvasHeight - artHeight) / 2;
		var rgba = new Color32[canvasWidth * canvasHeight];
		Array.Fill( rgba, new Color32( 255, 160, 0, 0 ) );
		for ( int row = 0; row < artHeight; row++ )
		for ( int col = 0; col < artWidth; col++ )
			rgba[(row + offsetY) * canvasWidth + col + offsetX] = new Color32( 255, 160, 0, alpha[(row + top) * width + col + left] );
		return Texture.Create( canvasWidth, canvasHeight ).WithName( Path ).WithData( rgba ).WithMips().Finish();
	}
}

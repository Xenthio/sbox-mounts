namespace GoldSrc;

public abstract partial class GameMount
{
	byte[] _palette;

	internal Color PaletteColor( int index )
	{
		if ( _palette is null )
		{
			var path = GameDirs.Select( dir => $"{dir}/gfx/palette.lmp" ).FirstOrDefault( FileSystem.FileExists );
			_palette = path is null ? [] : FileSystem.ReadAllBytes( path ).ToArray();
		}
		int offset = index * 3;
		return offset >= 0 && offset + 2 < _palette.Length
			? new Color( _palette[offset] / 255f, _palette[offset + 1] / 255f, _palette[offset + 2] / 255f ) : Color.White;
	}
}

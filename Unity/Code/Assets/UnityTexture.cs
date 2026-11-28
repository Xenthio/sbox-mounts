namespace UnityMount;

readonly record struct UnityTextureSampler( int Filter, int Anisotropy, float MipBias, int WrapU, int WrapV );

sealed record UnityTexture( string Name, int Width, int Height, int Format, int Mips, bool Linear,
	UnityBundle.Entry Entry, long Offset, int Length, bool Cube, int DecodedLength, UnityTextureSampler Sampler )
{
	public const int ClassId = 28;
	public const int CubemapClassId = 89;
	public const int Alpha8 = 1;
	public const int Rgb24 = 3;
	public const int Rgba32 = 4;
	public const int Dxt1 = 10;
	public const int Dxt5 = 12;
	public const int RgbaHalf = 17;
	public const int Bc6h = 24;
	public const int Bc7 = 25;
	public const int Bc4 = 26;
	public const int Bc5 = 27;
	const int Dxt1Crunched = 28;
	const int Dxt5Crunched = 29;
	public bool Hdr => Format is RgbaHalf or Bc6h;
	public int BlockFormat => GetBlockFormat( Format );
	public bool IsCrunched => BlockFormat != Format;
	public bool RequiresRgbaUpload => NeedsRgba( BlockFormat, Linear );

	public const int MaxPayloadSize = 128 * 1024 * 1024;

	public static UnityTexture Read( UnityBundle bundle, Stream stream, UnityBundle.Entry entry, SerializedFile.Object asset )
	{
		// Texture metadata in the Unity 2021.3 player schema. The name and platform
		// blob are variable length, but the image data itself need not be read to index it.
		using var data = new MemoryStream( bundle.ReadRange( stream, entry, asset.Offset, Math.Min( asset.Size, 16 * 1024 ) ), writable: false );
		using var reader = new UnityReader( data );
		var name = reader.ReadAlignedString();
		reader.Skip( 8 ); // Texture fallback settings.
		var width = reader.ReadInt32();
		var height = reader.ReadInt32();
		var completeSize = reader.ReadInt32();
		var strippedMips = reader.ReadInt32();
		var format = reader.ReadInt32();
		var mips = reader.ReadInt32();
		reader.Skip( 8 ); // Import flags and streaming priority.
		var images = reader.ReadInt32();
		var dimension = reader.ReadInt32();
		var sampler = new UnityTextureSampler( reader.ReadInt32(), reader.ReadInt32(), reader.ReadSingle(), reader.ReadInt32(), reader.ReadInt32() );
		reader.Skip( 8 ); // W wrap and lightmap format.
		var linear = reader.ReadInt32() == 0;
		var cube = asset.ClassId == CubemapClassId;
		var hdr = format is RgbaHalf or Bc6h;
		var blockFormat = GetBlockFormat( format );
		var crunched = blockFormat != format;

		if ( width is <= 0 or > 16384 || height is <= 0 or > 16384 ||
			(cube ? images != 6 || width != height || dimension is not (2 or 4) : images != 1 || dimension != 2) ||
			strippedMips != 0 ||
			mips <= 0 || mips > 1 + (int)Math.Log2( Math.Max( width, height ) ) ||
			(cube && crunched) ||
			(blockFormat is not (Rgba32 or Dxt1 or Dxt5 or Bc7) && !(cube ? hdr : format is Alpha8 or Rgb24 or Bc4 or Bc5)) )
			return null;

		var bytesPerTexel = hdr ? 8 : 4;
		if ( cube && (long)width * height * images * bytesPerTexel * 4 / 3 > MaxPayloadSize ) return null;
		var expectedSize = 0L;
		var rgbaSize = 0L;
		for ( var mip = 0; mip < mips; mip++ )
		{
			var w = Math.Max( width >> mip, 1 );
			var h = Math.Max( height >> mip, 1 );
			if ( !cube && !linear && (blockFormat is Dxt1 or Dxt5) && h > 4 && h % 4 != 0 ) return null;
			expectedSize += (long)images * MipSize( blockFormat, w, h );
			rgbaSize += (long)w * h * 4;
		}
		if ( expectedSize > MaxPayloadSize || (!cube && NeedsRgba( blockFormat, linear ) && rgbaSize > MaxPayloadSize) ) return null;
		var storedSize = crunched ? completeSize : (int)expectedSize;
		if ( storedSize is <= 0 or > MaxPayloadSize ) return null;

		reader.Skip( reader.ReadInt32() ); // Platform blob.
		reader.Align( 4 );
		var inlineSize = reader.ReadInt32();
		if ( inlineSize != 0 )
		{
			if ( inlineSize != storedSize || inlineSize > asset.Size - reader.Position ) return null;
			return new( name, width, height, format, mips, linear, entry, asset.Offset + reader.Position, inlineSize, cube, (int)expectedSize, sampler );
		}

		var offset = reader.ReadInt64();
		var size = reader.ReadInt32();
		var path = reader.ReadAlignedString();
		if ( size != storedSize || string.IsNullOrEmpty( path ) ) return null;
		var streamName = path[(path.LastIndexOf( '/' ) + 1)..];
		foreach ( var resource in bundle.Entries )
		{
			if ( resource.Serialized || !resource.Name.Equals( streamName, StringComparison.Ordinal ) ) continue;
			if ( offset < 0 || offset > resource.Length || size > resource.Length - offset ) return null;
			return new( name, width, height, format, mips, linear, resource, offset, size, cube, (int)expectedSize, sampler );
		}
		return null;
	}

	public bool SupportsPackedNormal => !Cube && (BlockFormat is Rgba32 or Dxt5 or Bc7 or Bc5) &&
		(long)Width * Height * 4 * 4 / 3 <= MaxPayloadSize;

	static int GetBlockFormat( int format ) => format switch
	{
		Dxt1Crunched => Dxt1,
		Dxt5Crunched => Dxt5,
		_ => format
	};

	static bool NeedsRgba( int format, bool linear ) => format is Rgb24 or Bc7 or Bc4 or Bc5 || (linear && format is Dxt1 or Dxt5);

	public static int MipSize( int format, int width, int height ) => format switch
	{
		Alpha8 => width * height,
		Rgb24 => width * height * 3,
		Rgba32 => width * height * 4,
		RgbaHalf => width * height * 8,
		_ => ((width + 3) / 4) * ((height + 3) / 4) * (format is Dxt1 or Bc4 ? 8 : 16)
	};
}

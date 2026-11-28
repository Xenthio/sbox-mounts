namespace UnityMount;

sealed class UnityCubemapLoader( UnityBundle bundle, UnityTexture asset ) : ResourceLoader<GameMount>
{
	protected override object Load()
	{
		var source = bundle.ReadRange( Host.FileSystem, asset.Entry, asset.Offset, asset.Length );
		var format = asset.Hdr ? ImageFormat.RGBA16161616F : asset.Linear ? ImageFormat.RGBA8888_LINEAR : ImageFormat.RGBA8888;
		return Texture.CreateCube( asset.Width, asset.Height, format )
			.WithName( Path )
			.WithData( ConvertFaces( source, asset ) )
			.WithMips( asset.Mips )
			.WithStaticUsage()
			.Finish();
	}

	static byte[] ConvertFaces( byte[] source, UnityTexture asset )
	{
		// Multimip s&box cubes require uncompressed data. Unity stores one mip chain per face.
		var sourceFaceSize = source.Length / 6;
		var bytesPerTexel = asset.Hdr ? 8 : 4;
		var outputSize = 0;
		for ( var mip = 0; mip < asset.Mips; mip++ )
		{
			var size = Math.Max( asset.Width >> mip, 1 );
			outputSize += size * size * bytesPerTexel * 6;
		}
		var target = new byte[outputSize];
		var decoded = asset.Format is UnityTexture.Rgba32 or UnityTexture.RgbaHalf ? null
			: new byte[asset.Width * asset.Height * bytesPerTexel];
		var sourceMipOffset = 0;
		var targetMipOffset = target.Length;
		for ( var mip = 0; mip < asset.Mips; mip++ )
		{
			var size = Math.Max( asset.Width >> mip, 1 );
			var sourceSize = UnityTexture.MipSize( asset.Format, size, size );
			var outputFaceSize = size * size * bytesPerTexel;
			targetMipOffset -= outputFaceSize * 6;
			for ( var face = 0; face < 6; face++ )
			{
				var sourceFace = SourceFaces[face];
				ReadOnlySpan<byte> input = source.AsSpan( sourceFace * sourceFaceSize + sourceMipOffset, sourceSize );
				if ( decoded is not null )
				{
					if ( asset.Format == UnityTexture.Bc6h ) UnityBc6h.ReadRgbaHalf( input, decoded, size );
					else if ( asset.Format == UnityTexture.Bc7 ) UnityBc7.ReadRgba( input, decoded, size, size, flipRows: false );
					else UnityBlockTexture.ReadRgba( input, decoded, size, size, asset.Format == UnityTexture.Dxt5 );
					input = decoded.AsSpan( 0, outputFaceSize );
				}
				WriteFace( input, target.AsSpan( targetMipOffset + face * outputFaceSize, outputFaceSize ), face, size, bytesPerTexel );
			}
			sourceMipOffset += sourceSize;
		}
		return target;
	}

	// Cube faces are ordered +X, -X, +Y, -Y, +Z, -Z in both APIs.
	// The Unity-to-Source basis maps (x, y, z) to (z, -x, y).
	static readonly int[] SourceFaces = [4, 5, 1, 0, 2, 3];

	static void WriteFace( ReadOnlySpan<byte> source, Span<byte> target, int face, int size, int bytesPerTexel )
	{
		var last = size - 1;
		for ( var y = 0; y < size; y++ )
		for ( var x = 0; x < size; x++ )
		{
			var (sx, sy) = face switch
			{
				1 => (last - y, last - x),
				2 => (x, last - y),
				3 => (last - x, y),
				_ => (y, x)
			};
			source.Slice( (sy * size + sx) * bytesPerTexel, bytesPerTexel )
				.CopyTo( target.Slice( (y * size + x) * bytesPerTexel, bytesPerTexel ) );
		}
	}
}

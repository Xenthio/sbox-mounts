using Sandbox.Rendering;

namespace UnityMount;

readonly record struct UnityMountedTexture( string Path, UnityTextureSampler Sampler )
{
	public float MirrorV => Sampler.WrapV == 3 ? 1 : 0;

	public int SamplerIndex
	{
		get
		{
			if ( Path is null ) return new SamplerState().Index;
			var source = Sampler;
			var anisotropy = Math.Clamp( source.Anisotropy, 1, 16 );
			var sampler = new SamplerState
			{
				Filter = source.Filter switch
				{
					0 => FilterMode.Point,
					2 when anisotropy > 1 => FilterMode.Anisotropic,
					2 => FilterMode.Trilinear,
					_ => FilterMode.Bilinear
				},
				MaxAnisotropy = anisotropy,
				MipLodBias = float.IsFinite( source.MipBias ) ? source.MipBias : 0,
				AddressModeU = AddressMode( source.WrapU ),
				AddressModeV = source.WrapV == 3 ? TextureAddressMode.Clamp : AddressMode( source.WrapV )
			};
			return sampler.Index;
		}
	}

	static TextureAddressMode AddressMode( int wrap ) => wrap switch
	{
		1 => TextureAddressMode.Clamp,
		2 => TextureAddressMode.Mirror,
		3 => TextureAddressMode.MirrorOnce,
		_ => TextureAddressMode.Wrap
	};
}

sealed class UnityTextureLoader( UnityBundle bundle, UnityTexture asset, bool packNormal ) : ResourceLoader<GameMount>
{
	protected override object Load()
	{
		var source = bundle.ReadRange( Host.FileSystem, asset.Entry, asset.Offset, asset.Length );
		var sourceFormat = asset.BlockFormat;
		if ( asset.IsCrunched )
			source = UnityCrunch.Decode( source, asset.Width, asset.Height, asset.Mips, sourceFormat == UnityTexture.Dxt5, asset.DecodedLength );
		var convert = packNormal || asset.RequiresRgbaUpload;
		var format = convert ? UnityTexture.Rgba32 : sourceFormat;
		var length = 0;
		for ( var mip = 0; mip < asset.Mips; mip++ )
			length += UnityTexture.MipSize( format, Math.Max( asset.Width >> mip, 1 ), Math.Max( asset.Height >> mip, 1 ) );

		var pixels = !convert && asset.Mips == 1 ? source : new byte[length];
		var unpackedNormal = packNormal && sourceFormat is UnityTexture.Bc7 or UnityTexture.Bc5
			? new byte[UnityTexture.MipSize( UnityTexture.Rgba32, asset.Width, asset.Height )] : null;
		var sourceOffset = 0;
		var targetOffset = length;
		for ( var mip = 0; mip < asset.Mips; mip++ )
		{
			var width = Math.Max( asset.Width >> mip, 1 );
			var height = Math.Max( asset.Height >> mip, 1 );
			var size = UnityTexture.MipSize( sourceFormat, width, height );
			// Unity stores the largest mip first. s&box expects the smallest mip first.
			var targetSize = UnityTexture.MipSize( format, width, height );
			targetOffset -= targetSize;
			var input = source.AsSpan( sourceOffset, size );
			var data = pixels.AsSpan( targetOffset, targetSize );
			sourceOffset += size;
			if ( packNormal )
			{
				if ( unpackedNormal is not null )
				{
					var rgba = unpackedNormal.AsSpan( 0, targetSize );
					ExpandTexture( input, rgba, width, height, sourceFormat, flipRows: false );
					UnityNormalMap.PackMip( rgba, data, width, height, UnityTexture.Rgba32 );
				}
				else UnityNormalMap.PackMip( input, data, width, height, sourceFormat );
				continue;
			}
			if ( asset.RequiresRgbaUpload )
			{
				ExpandTexture( input, data, width, height, sourceFormat, flipRows: true );
				continue;
			}

			input.CopyTo( data );
			if ( format == UnityTexture.Alpha8 )
				FlipRows( data, width, height );
			else if ( format == UnityTexture.Rgba32 )
				FlipRows( data, width * 4, height );
			else
				FlipCompressedRows( data, width, height, format == UnityTexture.Dxt5 );
		}

		var imageFormat = format switch
		{
			UnityTexture.Alpha8 => ImageFormat.A8,
			UnityTexture.Dxt1 => ImageFormat.DXT1,
			UnityTexture.Dxt5 => ImageFormat.DXT5,
			_ => (packNormal || asset.Linear || sourceFormat is UnityTexture.Bc4 or UnityTexture.Bc5)
				? ImageFormat.RGBA8888_LINEAR : ImageFormat.RGBA8888
		};
		return Texture.Create( asset.Width, asset.Height, imageFormat )
			.WithName( Path )
			.WithData( pixels )
			.WithMips( asset.Mips )
			.WithStaticUsage()
			.Finish();
	}

	static void ExpandTexture( ReadOnlySpan<byte> source, Span<byte> target, int width, int height, int format, bool flipRows )
	{
		switch ( format )
		{
			case UnityTexture.Rgb24:
				ExpandRgb( source, target, width, height );
				break;
			case UnityTexture.Bc7:
				UnityBc7.ReadRgba( source, target, width, height, flipRows );
				break;
			case UnityTexture.Bc4:
			case UnityTexture.Bc5:
				UnityBlockTexture.ReadRg( source, target, width, height, format == UnityTexture.Bc5, flipRows );
				break;
			default:
				UnityBlockTexture.ReadRgba( source, target, width, height, format == UnityTexture.Dxt5, flipRows );
				break;
		}
	}

	static void ExpandRgb( ReadOnlySpan<byte> source, Span<byte> target, int width, int height )
	{
		for ( var y = 0; y < height; y++ )
		for ( var x = 0; x < width; x++ )
		{
			var input = ((height - 1 - y) * width + x) * 3;
			var output = (y * width + x) * 4;
			source.Slice( input, 3 ).CopyTo( target[output..] );
			target[output + 3] = 255;
		}
	}

	static void FlipRows( Span<byte> data, int rowSize, int rows )
	{
		for ( var top = 0; top < rows / 2; top++ )
		{
			var a = data.Slice( top * rowSize, rowSize );
			var b = data.Slice( (rows - top - 1) * rowSize, rowSize );
			for ( var i = 0; i < rowSize; i++ ) (a[i], b[i]) = (b[i], a[i]);
		}
	}

	static void FlipCompressedRows( Span<byte> data, int width, int height, bool hasAlpha )
	{
		var blockSize = hasAlpha ? 16 : 8;
		FlipRows( data, ((width + 3) / 4) * blockSize, (height + 3) / 4 );
		var rowsPerBlock = Math.Min( 4, height );

		for ( var offset = 0; offset < data.Length; offset += blockSize )
		{
			var block = data.Slice( offset, blockSize );
			if ( hasAlpha ) FlipAlphaRows( block, rowsPerBlock );
			// Each BC1 color index row occupies one byte. BC3 has an eight-byte alpha block first.
			block.Slice( hasAlpha ? 12 : 4, rowsPerBlock ).Reverse();
		}
	}

	static void FlipAlphaRows( Span<byte> block, int rows )
	{
		// BC3 packs four three-bit alpha indices per row after the two endpoints.
		ulong indices = 0;
		for ( var i = 0; i < 6; i++ ) indices |= (ulong)block[i + 2] << (i * 8);
		var reversed = indices;
		for ( var row = 0; row < rows; row++ )
		{
			var input = row * 12;
			var output = (rows - row - 1) * 12;
			reversed = (reversed & ~(0xfffUL << output)) | (((indices >> input) & 0xfffUL) << output);
		}
		for ( var i = 0; i < 6; i++ ) block[i + 2] = (byte)(reversed >> (i * 8));
	}
}

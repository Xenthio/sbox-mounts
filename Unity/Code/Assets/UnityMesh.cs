using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace UnityMount;

[StructLayout( LayoutKind.Sequential )]
struct UnityMeshVertex
{
	[VertexLayout.Position] public Vector3 Position;
	[VertexLayout.Normal] public Vector3 Normal;
	[VertexLayout.Tangent] public Vector4 Tangent;
	[VertexLayout.TexCoord( 0 )] public Vector2 TexCoord;
	[VertexLayout.TexCoord( 1 )] public Vector2 SecondaryTexCoord;
	[VertexLayout.Color] public Color32 Color;
}

sealed partial class UnityMesh
{
	public const int ClassId = 43;
	public const int FilterClassId = 33;
	public const int RendererClassId = 23;
	public const int MaxObjectSize = 64 * 1024 * 1024;

	readonly record struct SubMesh( int FirstByte, int IndexCount, int BaseVertex );
	readonly record struct StreamLayout( int Offset, int Stride );
	readonly record struct Channel( byte Stream, byte Offset, byte Format, byte Components )
	{
		public bool IsFloat => Format is 0 or 1;
	}

	public readonly record struct BoneInfluence( Color32 Indices, Color32 Weights );

	public System.Numerics.Matrix4x4[] BindPoses { get; }
	public BoneInfluence[] Influences { get; }
	public BBox?[] BoneBounds { get; }
	public UnityMeshVertex[] Vertices { get; }
	public int[][] Indices { get; }

	UnityMesh( UnityMeshVertex[] vertices, int[][] indices, System.Numerics.Matrix4x4[] bindPoses, BoneInfluence[] influences, BBox?[] boneBounds = null )
	{
		Vertices = vertices;
		Indices = indices;
		BindPoses = bindPoses;
		Influences = influences;
		BoneBounds = boneBounds ?? [];
	}

	public static UnityMesh Read( UnityAssetPointer source, UnitySkin skin = null )
	{
		using var stream = source.File.OpenBundle();
		return Read( source.File.Bundle, stream, source.File.Entry, source.Asset, skin );
	}

	public static UnityMesh Read( UnityBundle bundle, Stream stream, UnityBundle.Entry entry, SerializedFile.Object asset, UnitySkin skin = null )
	{
		if ( asset.Tree is null || asset.Size > MaxObjectSize ) return null;
		using var data = new MemoryStream( bundle.ReadRange( stream, entry, asset.Offset, asset.Size ), writable: false );
		using var reader = new UnityReader( data );

		var boneCount = skin?.Count ?? 0;
		var bindPoses = ReadBindPoses( reader, asset.Tree, skin, out var boneRemap );
		if ( bindPoses is null ) return null;

		var submeshes = ReadSubMeshes( reader, asset.Tree );
		if ( submeshes is null || !submeshes.Any( surface => surface.IndexCount > 0 ) ) return null;

		reader.Position = 0;
		asset.Tree.SeekField( reader, "m_MeshCompression" );
		var compressed = reader.ReadByte() != 0;

		reader.Position = 0;
		asset.Tree.SeekField( reader, "m_IndexFormat" );
		var indexSize = reader.ReadInt32() switch { 0 => 2, 1 => 4, _ => 0 };
		if ( indexSize == 0 ) return null;
		if ( compressed ) return boneCount == 0 ? ReadCompressed( reader, asset.Tree, submeshes, indexSize ) : null;
		var indices = reader.ReadExactBytes( reader.ReadInt32() );

		reader.Position = 0;
		asset.Tree.SeekField( reader, "m_VertexData" );
		var vertexCount = reader.ReadInt32();
		var channelCount = reader.ReadInt32();
		if ( vertexCount is <= 0 or > 1000000 || channelCount is < 5 or > 32 ) return null;
		var channels = new Channel[channelCount];
		for ( var i = 0; i < channels.Length; i++ )
			channels[i] = new( reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), (byte)(reader.ReadByte() & 15) );

		var vertexBytes = reader.ReadInt32();
		var buffer = vertexBytes == 0
			? ReadStreamedVertices( bundle, stream, asset.Tree, reader )
			: reader.ReadExactBytes( vertexBytes );
		if ( buffer is null ) return null;

		var position = channels[0];
		var normal = channels[1];
		var tangent = channels[2];
		var color = channels[3];
		var uv = channels[4];
		var secondaryUv = channelCount > 5 ? channels[5] : default;
		var hasSecondaryUv = secondaryUv.IsFloat && secondaryUv.Components is >= 2 and <= 4;
		var streams = GetStreams( channels, vertexCount, buffer.Length );
		if ( streams is null || !ValidChannel( position, 3 ) || !normal.IsFloat || normal.Components is not (3 or 4) ||
			uv.Components != 0 && (!uv.IsFloat || uv.Components is < 2 or > 4) ||
			tangent.Components != 0 && !ValidChannel( tangent, 4 ) ) return null;

		var influences = boneCount > 0 ? new BoneInfluence[vertexCount] : [];
		var blendWeights = channelCount > 12 ? channels[12] : default;
		var blendIndices = channelCount > 13 ? channels[13] : default;
		if ( boneCount > 0 && (blendIndices.Components is < 1 or > 4 || blendIndices.Format is not (6 or 8 or 10) ||
			(blendIndices.Components != 1 || blendWeights.Components != 0) && !ValidChannel( blendWeights, blendIndices.Components )) ) return null;

		var vertices = new UnityMeshVertex[vertexCount];
		for ( var i = 0; i < vertices.Length; i++ )
		{
			var point = ReadVector( position );
			var n = ReadVector( normal );
			var t = tangent.Components == 0
				? new Vector4( Vector3.Cross( MathF.Abs( n.z ) < 0.99f ? Vector3.Up : Vector3.Forward, n ).Normal, 1 )
				: new Vector4( ReadVector( tangent ), ReadComponent( tangent, 3 ) );
			if ( !float.IsFinite( point.LengthSquared ) || !float.IsFinite( n.LengthSquared ) ||
				!float.IsFinite( t.LengthSquared ) ) return null;
			if ( boneCount > 0 && !TryReadInfluence( i, buffer, streams, blendIndices, blendWeights, boneCount, boneRemap, out influences[i] ) ) return null;
			vertices[i] = new UnityMeshVertex
			{
				Position = UnityCoordinates.ToInches( point ),
				Normal = n.Normal,
				Tangent = t,
				TexCoord = uv.Components == 0 ? Vector2.Zero : ReadUv( uv ),
				SecondaryTexCoord = hasSecondaryUv ? ReadUv( secondaryUv ) : Vector2.Zero,
				Color = color.Components == 4 && color.Format is 0 or 1 or 2
					? new Color32( ReadColor( 0 ), ReadColor( 1 ), ReadColor( 2 ), ReadColor( 3 ) ) : Color32.White
			};

			Vector3 ReadVector( Channel channel ) => UnityCoordinates.Direction(
				new System.Numerics.Vector3( ReadComponent( channel, 0 ), ReadComponent( channel, 1 ), ReadComponent( channel, 2 ) ) );

			Vector2 ReadUv( Channel channel )
			{
				// Triplanar assets can contain undefined UVs without invalidating the geometry.
				var u = ReadComponent( channel, 0 );
				var v = ReadComponent( channel, 1 );
				return new( float.IsFinite( u ) ? u : 0, float.IsFinite( v ) ? 1 - v : 0 );
			}

			float ReadComponent( Channel channel, int component )
			{
				var bytes = ChannelData( buffer, streams, channel, i, component );
				return channel.Format == 1 ? (float)BinaryPrimitives.ReadHalfLittleEndian( bytes ) : BinaryPrimitives.ReadSingleLittleEndian( bytes );
			}

			byte ReadColor( int component ) => color.Format == 2
				? ChannelData( buffer, streams, color, i, component )[0] : ToColorByte( ReadComponent( color, component ) );
		}

		var surfaces = ReadSurfaces( submeshes, indexSize, indices.Length / indexSize, vertexCount, index =>
		{
			var bytes = indices.AsSpan( index * indexSize, indexSize );
			return indexSize == 2 ? BinaryPrimitives.ReadUInt16LittleEndian( bytes ) : BinaryPrimitives.ReadUInt32LittleEndian( bytes );
		} );
		return surfaces is null ? null : new( vertices, surfaces, bindPoses, influences, ReadBoneBounds( reader, asset.Tree, boneCount, boneRemap ) );

		static bool ValidChannel( Channel channel, int components ) => channel.IsFloat && channel.Components == components;
	}

	static byte ToColorByte( float value ) => float.IsFinite( value ) ? (byte)MathF.Round( Math.Clamp( value, 0, 1 ) * 255 ) : (byte)0;

	public static bool HasSurfaces( UnityReader reader, UnityTypeTree tree ) =>
		ReadSubMeshes( reader, tree )?.Any( surface => surface.IndexCount > 0 ) == true;

	static SubMesh[] ReadSubMeshes( UnityReader reader, UnityTypeTree tree )
	{
		reader.Position = 0;
		tree.SeekField( reader, "m_SubMeshes" );
		var count = reader.ReadInt32();
		if ( count <= 0 || count > reader.Remaining / 48 ) return null;
		var submeshes = new SubMesh[count];
		for ( var i = 0; i < submeshes.Length; i++ )
		{
			var first = reader.ReadInt32();
			var indices = reader.ReadInt32();
			var topology = reader.ReadInt32();
			var baseVertex = reader.ReadInt32();
			if ( topology != 0 || indices < 0 || indices % 3 != 0 || first < 0 || baseVertex < 0 ) return null;
			submeshes[i] = new( first, indices, baseVertex );
			reader.Skip( 32 ); // First vertex, vertex count and local bounds.
		}
		return submeshes;
	}

	static int[][] ReadSurfaces( SubMesh[] submeshes, int indexSize, int indexCount, int vertexCount, Func<int, uint> read )
	{
		var surfaces = new int[submeshes.Length][];
		for ( var surface = 0; surface < submeshes.Length; surface++ )
		{
			var submesh = submeshes[surface];
			var first = submesh.FirstByte / indexSize;
			if ( submesh.FirstByte % indexSize != 0 || first > indexCount || submesh.IndexCount > indexCount - first )
				return null;
			var output = new int[submesh.IndexCount];
			for ( var i = 0; i < output.Length; i++ )
			{
				var index = (long)read( first + i ) + submesh.BaseVertex;
				if ( index >= vertexCount ) return null;
				output[i] = (int)index;
			}
			// Changing handedness reverses each triangle's front face.
			for ( var i = 0; i < output.Length; i += 3 )
				(output[i + 1], output[i + 2]) = (output[i + 2], output[i + 1]);
			surfaces[surface] = output;
		}
		return surfaces;
	}

	static System.Numerics.Matrix4x4[] ReadBindPoses( UnityReader reader, UnityTypeTree tree, UnitySkin skin, out int[] boneRemap )
	{
		boneRemap = null;
		if ( skin is null ) return [];
		tree.SeekField( reader, "m_BindPose" );
		var count = reader.ReadInt32();
		if ( count < skin.Count || count > UnitySkin.MaxBones || count > reader.Remaining / 64 ) return null;
		if ( count > skin.Count )
		{
			// Some prefabs omit unused FBX joints. Match the remaining bind poses by path.
			if ( !tree.HasField( "m_BoneNameHashes" ) ) return null;
			var start = reader.Position;
			reader.Position = 0;
			tree.SeekField( reader, "m_BoneNameHashes" );
			var hashes = UnityAvatar.ReadHashes( reader, UnitySkin.MaxBones );
			if ( hashes.Length != count || (boneRemap = skin.RemapMeshBones( hashes )) is null ) return null;
			reader.Position = start;
		}
		var poses = new System.Numerics.Matrix4x4[skin.Count];
		for ( var i = 0; i < count; i++ )
		{
			var slot = boneRemap is null ? i : boneRemap[i];
			if ( slot >= 0 ) poses[slot] = UnitySkin.ReadBindPose( reader );
			else reader.Skip( 64 );
		}
		return poses;
	}

	static BBox?[] ReadBoneBounds( UnityReader reader, UnityTypeTree tree, int boneCount, int[] boneRemap )
	{
		if ( boneCount == 0 || !tree.HasField( "m_BonesAABB" ) ) return [];

		reader.Position = 0;
		tree.SeekField( reader, "m_BonesAABB" );
		var count = reader.ReadInt32();
		if ( count != (boneRemap?.Length ?? boneCount) || count > reader.Remaining / 24 ) return [];

		var bounds = new BBox?[boneCount];
		for ( var i = 0; i < count; i++ )
		{
			var min = new System.Numerics.Vector3( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
			var max = new System.Numerics.Vector3( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
			// Unity uses an inverted, infinite box for joints that do not influence vertices.
			var slot = boneRemap is null ? i : boneRemap[i];
			if ( slot < 0 || !float.IsFinite( min.LengthSquared() ) || !float.IsFinite( max.LengthSquared() ) ||
				min.X > max.X || min.Y > max.Y || min.Z > max.Z ) continue;

			var lower = UnityCoordinates.ToInches( new Vector3( min.Z, -max.X, min.Y ) );
			var upper = UnityCoordinates.ToInches( new Vector3( max.Z, -min.X, max.Y ) );
			bounds[slot] = new( lower, upper );
		}
		return bounds;
	}

	static ReadOnlySpan<byte> ChannelData( byte[] buffer, StreamLayout[] streams, Channel channel, int vertex, int component )
	{
		var layout = streams[channel.Stream];
		var size = ElementSize( channel.Format );
		var offset = layout.Offset + vertex * layout.Stride + channel.Offset + component * size;
		return buffer.AsSpan( offset, size );
	}

	static bool TryReadInfluence( int vertex, byte[] buffer, StreamLayout[] streams, Channel indices, Channel weights,
		int boneCount, int[] boneRemap, out BoneInfluence influence )
	{
		influence = default;
		Span<byte> bones = stackalloc byte[4];
		Span<float> sourceWeights = stackalloc float[4];
		bones.Clear();
		sourceWeights.Clear();
		var total = 0f;
		for ( var i = 0; i < indices.Components; i++ )
		{
			var weight = 1f;
			if ( weights.Components > 0 )
			{
				var weightBytes = ChannelData( buffer, streams, weights, vertex, i );
				weight = weights.Format == 1 ? (float)BinaryPrimitives.ReadHalfLittleEndian( weightBytes )
					: BinaryPrimitives.ReadSingleLittleEndian( weightBytes );
			}
			if ( !float.IsFinite( weight ) || weight < 0 ) return false;
			if ( weight == 0 ) continue;

			var bytes = ChannelData( buffer, streams, indices, vertex, i );
			var bone = indices.Format switch { 6 => bytes[0], 8 => BinaryPrimitives.ReadUInt16LittleEndian( bytes ), _ => BinaryPrimitives.ReadUInt32LittleEndian( bytes ) };
			if ( bone >= (boneRemap?.Length ?? boneCount) ) return false;
			var slot = boneRemap is null ? (int)bone : boneRemap[bone];
			if ( slot < 0 ) return false;
			bones[i] = (byte)slot;
			sourceWeights[i] = weight;
			total += weight;
		}
		if ( total <= 0 || !float.IsFinite( total ) ) return false;

		Span<byte> normalized = stackalloc byte[4];
		var cumulative = 0f;
		var previous = 0;
		for ( var i = 0; i < normalized.Length; i++ )
		{
			cumulative += sourceWeights[i];
			var next = (int)MathF.Round( 255 * (cumulative / total) );
			normalized[i] = (byte)(next - previous);
			previous = next;
		}
		influence = new( new( bones[0], bones[1], bones[2], bones[3] ), new( normalized[0], normalized[1], normalized[2], normalized[3] ) );
		return true;
	}

	static int ElementSize( byte format ) => format switch
	{
		0 or 10 or 11 => 4,
		1 or 4 or 5 or 8 or 9 => 2,
		2 or 3 or 6 or 7 => 1,
		_ => throw new NotSupportedException( $"Unity vertex format {format}." )
	};

	static StreamLayout[] GetStreams( Channel[] channels, int vertexCount, int bufferLength )
	{
		var layouts = new StreamLayout[channels.Max( channel => channel.Stream ) + 1];
		foreach ( var channel in channels )
		{
			if ( channel.Components == 0 ) continue;
			var end = channel.Offset + channel.Components * ElementSize( channel.Format );
			ref var layout = ref layouts[channel.Stream];
			layout = new( 0, Math.Max( layout.Stride, end ) );
		}

		long start = 0;
		for ( var i = 0; i < layouts.Length; i++ )
		{
			var stride = layouts[i].Stride;
			if ( stride == 0 ) continue;
			var end = start + (long)stride * vertexCount;
			if ( end > bufferLength ) return null;
			layouts[i] = new( (int)start, stride );
			start = (end + 15) & ~15L; // Unity aligns the next vertex stream to 16 bytes.
		}
		return layouts;
	}

	static byte[] ReadStreamedVertices( UnityBundle bundle, Stream stream, UnityTypeTree tree, UnityReader reader )
	{
		reader.Position = 0;
		tree.SeekField( reader, "m_StreamData" );
		var offset = reader.ReadInt64();
		var size = reader.ReadInt32();
		var path = reader.ReadAlignedString();
		if ( size <= 0 || size > MaxObjectSize || string.IsNullOrEmpty( path ) ) return null;
		var name = path[(path.LastIndexOf( '/' ) + 1)..];
		foreach ( var entry in bundle.Entries )
			if ( !entry.Serialized && entry.Name == name ) return bundle.ReadRange( stream, entry, offset, size );
		return null;
	}
}

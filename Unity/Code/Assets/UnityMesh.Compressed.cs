namespace UnityMount;

sealed partial class UnityMesh
{
	readonly record struct PackedValues( int Count, int Bits, byte[] Data, float Start, float Range )
	{
		uint Mask => Bits == 32 ? uint.MaxValue : (1u << Bits) - 1;

		public uint ReadInt( int index )
		{
			if ( Bits == 0 ) return 0;
			var offset = (long)index * Bits;
			var shift = (int)(offset & 7);
			var start = (int)(offset >> 3);
			var length = (Bits + shift + 7) / 8;
			ulong bytes = 0;
			for ( var i = 0; i < length; i++ ) bytes |= (ulong)Data[start + i] << (8 * i);
			return (uint)(bytes >> shift) & Mask;
		}

		public float ReadFloat( int index ) => Bits == 0 ? Start : Start + Range * (ReadInt( index ) / (float)Mask);
	}

	static UnityMesh ReadCompressed( UnityReader reader, UnityTypeTree tree, SubMesh[] submeshes, int indexSize )
	{
		reader.Position = 0;
		var packed = tree.SeekField( reader, "m_CompressedMesh" );
		var start = reader.Position;
		var positions = ReadValues( "m_Vertices", true );
		if ( positions.Count % 3 != 0 ) return null;
		var count = positions.Count / 3;
		if ( count is <= 0 or > 1000000 ) return null;

		var normals = ReadValues( "m_Normals", true );
		var normalSigns = ReadValues( "m_NormalSigns", false );
		if ( normals.Count != count * 2 || normalSigns.Count != count ) return null;
		var tangents = ReadValues( "m_Tangents", true );
		var tangentSigns = ReadValues( "m_TangentSigns", false );
		if ( tangents.Count != 0 && (tangents.Count != count * 2 || tangentSigns.Count != count * 2) ) return null;
		var uv = ReadValues( "m_UV", true );
		reader.Position = start;
		packed.SeekField( reader, "m_UVInfo" );
		var uvInfo = reader.ReadUInt32();
		var uvWidth = uvInfo == 0 ? 2 : (uvInfo & 4) == 0 ? 0 : (int)(uvInfo & 3) + 1;
		var uv1Width = (uvInfo & 0x40) == 0 ? 0 : (int)((uvInfo >> 4) & 3) + 1;
		var hasUv = uv.Count > 0 && uvWidth > 0;
		if ( (uv.Count > 0 || uvInfo != 0) && uv.Count < count * (uvWidth + uv1Width) ) return null;
		var colors = ReadValues( "m_FloatColors", true );
		if ( colors.Count != 0 && colors.Count != count * 4 ) return null;

		var triangles = ReadValues( "m_Triangles", false );
		var surfaces = ReadSurfaces( submeshes, indexSize, triangles.Count, count, triangles.ReadInt );
		if ( surfaces is null ) return null;

		var vertices = new UnityMeshVertex[count];
		for ( var i = 0; i < count; i++ )
		{
			var point = new System.Numerics.Vector3( positions.ReadFloat( i * 3 ), positions.ReadFloat( i * 3 + 1 ), positions.ReadFloat( i * 3 + 2 ) );
			var normal = ReadDirection( normals, i * 2, normalSigns.ReadInt( i ) );
			var tangent = tangents.Count > 0 ? ReadDirection( tangents, i * 2, tangentSigns.ReadInt( i * 2 ) )
				: Vector3.Cross( MathF.Abs( normal.z ) < 0.99f ? Vector3.Up : Vector3.Forward, normal ).Normal;
			var u = hasUv ? uv.ReadFloat( i * uvWidth ) : 0;
			var v = hasUv && uvWidth > 1 ? uv.ReadFloat( i * uvWidth + 1 ) : 1;
			var uv1 = count * uvWidth + i * uv1Width;
			vertices[i] = new()
			{
				Position = UnityCoordinates.Position( point ),
				Normal = normal,
				Tangent = new( tangent, tangents.Count > 0 && tangentSigns.ReadInt( i * 2 + 1 ) == 0 ? -1 : 1 ),
				TexCoord = new( u, 1 - v ),
				SecondaryTexCoord = uv1Width == 0 ? Vector2.Zero : new( uv.ReadFloat( uv1 ), uv1Width > 1 ? 1 - uv.ReadFloat( uv1 + 1 ) : 0 ),
				Color = colors.Count == 0 ? Color32.White : new Color32( ReadColor( i, 0 ), ReadColor( i, 1 ), ReadColor( i, 2 ), ReadColor( i, 3 ) )
			};
		}
		return new( vertices, surfaces, [], [] );

		byte ReadColor( int vertex, int component ) => ToColorByte( colors.ReadFloat( vertex * 4 + component ) );

		PackedValues ReadValues( string field, bool quantized )
		{
			reader.Position = start;
			var layout = packed.SeekField( reader, field );
			layout.SeekField( reader, "m_NumItems" );
			var items = reader.ReadInt32();
			var range = quantized ? reader.ReadSingle() : 0;
			var min = quantized ? reader.ReadSingle() : 0;
			var size = reader.ReadInt32();
			if ( items < 0 || size < 0 || size > reader.Remaining || !float.IsFinite( min ) ||
				!float.IsFinite( range ) || range < 0 || !float.IsFinite( min + range ) )
				throw new InvalidDataException( "Invalid Unity compressed mesh values." );
			var bytes = reader.ReadExactBytes( size );
			reader.Align( 4 );
			var bits = reader.ReadByte();
			if ( bits > 32 || (long)items * bits > (long)size * 8 )
				throw new InvalidDataException( "Invalid Unity compressed mesh bit width." );
			return new( items, bits, bytes, min, range );
		}
	}

	static Vector3 ReadDirection( PackedValues values, int offset, uint sign )
	{
		var x = values.ReadFloat( offset );
		var y = values.ReadFloat( offset + 1 );
		var z = MathF.Sqrt( MathF.Max( 0, 1 - x * x - y * y ) );
		return UnityCoordinates.Direction( new( x, y, sign == 0 ? -z : z ) ).Normal;
	}
}

namespace UnityMount;

sealed class UnityWavyClothMaterial
{
	public UnityReference ColorMap { get; private set; }
	public UnityReference NormalMap { get; private set; }
	public Vector4 ColorTransform { get; private set; } = new( 1, 1, 0, 0 );
	public Vector4 NormalTransform { get; private set; } = new( 1, 1, 0, 0 );
	public Vector4 Tint { get; private set; } = new( .5f, .5f, .5f, 1 );
	public Vector4 Scroll { get; private set; }
	public Vector3 Wind { get; private set; }
	public float Cutoff { get; private set; } = .5f;
	public float NormalStrength { get; private set; }
	public float VertexOffsetStrength { get; private set; }
	public float Smoothness { get; private set; }
	public bool VertexRed { get; private init; }

	public static bool Supports( string shader, string[] keywords ) =>
		shader == "Hopoo Games/Deferred/Wavy Cloth" && keywords.All( key => key == "VERTEX_RED_FOR_DISTORTION" );

	public static UnityWavyClothMaterial Read( UnityReader reader, UnityTypeTree properties, long start, string[] keywords )
	{
		var material = new UnityWavyClothMaterial { VertexRed = keywords.Contains( "VERTEX_RED_FOR_DISTORTION" ) };
		reader.Position = start;
		properties.SeekField( reader, "m_TexEnvs" ).VisitArray( reader, (_, _) =>
		{
			var name = reader.ReadAlignedString();
			var reference = UnityReference.Read( reader );
			var transform = new Vector4( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
			switch ( name )
			{
				case "_MainTex":
					material.ColorMap = reference;
					material.ColorTransform = transform;
					break;
				case "_ScrollingNormalMap":
					material.NormalMap = reference;
					material.NormalTransform = transform;
					break;
			}
		} );

		reader.Position = start;
		properties.SeekField( reader, "m_Colors" ).VisitArray( reader, (_, _) =>
		{
			var name = reader.ReadAlignedString();
			var value = new Vector4( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
			switch ( name )
			{
				case "_Color": material.Tint = value; break;
				case "_Scroll": material.Scroll = value; break;
				case "_WindVector": material.Wind = UnityCoordinates.Position( new( value.x, value.y, value.z ) ); break;
			}
		} );

		reader.Position = start;
		properties.SeekField( reader, "m_Floats" ).VisitArray( reader, (_, _) =>
		{
			var name = reader.ReadAlignedString();
			var value = reader.ReadSingle();
			if ( !float.IsFinite( value ) ) return;
			switch ( name )
			{
				case "_Cutoff": material.Cutoff = value; break;
				case "_NormalStrength": material.NormalStrength = value; break;
				case "_VertexOffsetStrength": material.VertexOffsetStrength = UnityCoordinates.ToInches( value ); break;
				case "_Smoothness": material.Smoothness = value; break;
			}
		} );
		return material;
	}
}

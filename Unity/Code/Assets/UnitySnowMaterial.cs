namespace UnityMount;

sealed class UnitySnowMaterial
{
	const string ShaderName = "Hopoo Games/Deferred/Snow Topped";

	public enum Map { Base, BaseNormal, Cover, CoverNormal, Dirt, DirtNormal }
	public readonly record struct Layer( UnityReference Reference, Vector4 Transform );
	public Layer[] Layers { get; } = Enumerable.Repeat( new Layer( default, new( 1, 1, 0, 0 ) ), 6 ).ToArray();
	public Color Tint { get; private set; } = new( .5f, .5f, .5f );
	public Vector2 Smoothness { get; private set; }
	public float Bias { get; private set; }
	public float BlendDepth { get; private set; } = .2f;
	public float NormalStrength { get; private set; } = 1;
	public float Frequency { get; private set; } = 1;
	public float Fade { get; private set; } = 1;
	public bool Triplanar { get; private set; }
	public bool BinaryBlend { get; private set; }
	public bool Dirt { get; private set; }
	public float DirtBias { get; private set; }
	public float DirtSmoothness { get; private set; }
	public bool Dither { get; private set; }
	public bool Microfacet { get; private set; }

	public static bool Supports( string shader, string[] keywords ) =>
		shader == ShaderName && !keywords.Any( key => key is "IGNORE_BIAS" or "GRADIENTBIAS" or "VERTEXCOLOR" );

	public static UnitySnowMaterial Read( UnityReader reader, UnityTypeTree properties, long start, string[] keywords )
	{
		var material = new UnitySnowMaterial
		{
			Triplanar = keywords.Contains( "TRIPLANAR" ),
			BinaryBlend = keywords.Contains( "BINARYBLEND" ),
			Dirt = keywords.Contains( "DIRTON" ),
			Dither = keywords.Contains( "DITHER" ),
			Microfacet = keywords.Contains( "MICROFACET_SNOW" )
		};

		reader.Position = start;
		properties.SeekField( reader, "m_TexEnvs" ).VisitArray( reader, (_, _) =>
		{
			var name = reader.ReadAlignedString();
			var reference = UnityReference.Read( reader );
			var transform = new Vector4( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
			Map? slot = name switch
			{
				"_MainTex" => Map.Base,
				"_NormalTex" => Map.BaseNormal,
				"_SnowTex" => Map.Cover,
				"_SnowNormalTex" => Map.CoverNormal,
				"_DirtTex" => Map.Dirt,
				"_DirtNormalTex" => Map.DirtNormal,
				_ => null
			};
			if ( slot is { } layer ) material.Layers[(int)layer] = new( reference, transform );
		} );

		reader.Position = start;
		properties.SeekField( reader, "m_Colors" ).VisitArray( reader, (_, _) =>
		{
			var name = reader.ReadAlignedString();
			var color = new Color( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
			if ( name == "_Color" ) material.Tint = color;
		} );

		var smoothness = Vector2.Zero;
		reader.Position = start;
		properties.SeekField( reader, "m_Floats" ).VisitArray( reader, (_, _) =>
		{
			var name = reader.ReadAlignedString();
			var value = reader.ReadSingle();
			if ( !float.IsFinite( value ) ) return;
			switch ( name )
			{
				case "_Smoothness": smoothness.x = value; break;
				case "_SnowSmoothness": smoothness.y = value; break;
				case "_SnowBias": material.Bias = value; break;
				case "_DirtBias": material.DirtBias = value; break;
				case "_DirtSmoothness": material.DirtSmoothness = value; break;
				case "_Depth": material.BlendDepth = MathF.Max( 0, value ); break;
				case "_NormalStrength": material.NormalStrength = value; break;
				case "_TriplanarTextureFactor": material.Frequency = value; break;
				case "_Fade" when material.Dither: material.Fade = Math.Clamp( value, 0, 1 ); break;
			}
		} );
		material.Smoothness = smoothness;
		return material;
	}
}

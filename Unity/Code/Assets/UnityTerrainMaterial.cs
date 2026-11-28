namespace UnityMount;

sealed class UnityTerrainMaterial
{
	const string ShaderName = "Hopoo Games/Deferred/Triplanar Terrain Blend";
	const string OptimizedShader = "Hopoo Games/Optimized/Switch/Environment/TerrainTriplanar";

	public enum Map { RedTop, RedSide, Green, Blue, Splat, Normal }
	public readonly record struct Layer( UnityReference Reference, Vector4 Transform );
	public readonly record struct OptimizedParameters( Vector3 Tiling, Vector3 Albedo, Vector3 VertexPower, Vector4 UvTransform );
	public OptimizedParameters? Optimized { get; private set; }
	public Layer[] Layers { get; } = Enumerable.Repeat( new Layer( default, new( 1, 1, 0, 0 ) ), 6 ).ToArray();
	public Color Tint { get; private set; } = new( .5f, .5f, .5f );
	public Vector3 Bias { get; private set; }
	public Vector3 Smoothness { get; private set; }
	public float Frequency { get; private set; } = 1;
	public float BlendDepth { get; private set; } = .2f;
	public float NormalStrength { get; private set; } = 1;
	public int Cull { get; private set; } = 2;
	public bool VertexColors { get; private set; }
	public bool MixVertexColors { get; private set; }
	public bool AlphaMask { get; private set; }
	public bool VerticalBias { get; private set; }
	public bool DoubleSample { get; private set; }
	public bool Microfacet { get; private set; }

	public static bool Supports( string shader ) => shader is ShaderName or OptimizedShader;

	public static UnityTerrainMaterial Read( UnityReader reader, UnityTypeTree properties, long start, string[] keywords, string shader )
	{
		var optimized = shader == OptimizedShader;
		var parameters = new OptimizedParameters( Vector3.One, Vector3.One, Vector3.One, new( 1, 1, 0, 0 ) );
		var material = new UnityTerrainMaterial
		{
			VertexColors = keywords.Contains( "USE_VERTEX_COLORS" ),
			MixVertexColors = keywords.Contains( "MIX_VERTEX_COLORS" ),
			AlphaMask = keywords.Contains( "USE_ALPHA_AS_MASK" ),
			VerticalBias = keywords.Contains( "USE_VERTICAL_BIAS" ),
			DoubleSample = keywords.Contains( "DOUBLESAMPLE" ),
			Microfacet = keywords.Contains( "MICROFACET_SNOW" )
		};
		reader.Position = start;
		properties.SeekField( reader, "m_TexEnvs" ).VisitArray( reader, (_, _) =>
		{
			var name = reader.ReadAlignedString();
			var reference = UnityReference.Read( reader );
			var st = new Vector4( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
			Map? slot = name switch
			{
				"_RedChannelTopTex" => Map.RedTop,
				"_RedChannelSideTex" => Map.RedSide,
				"_GreenChannelTex" => Map.Green,
				"_BlueChannelTex" => Map.Blue,
				"_SplatmapTex" => Map.Splat,
				"_NormalTex" => Map.Normal,
				_ => null
			};
			if ( slot.HasValue ) material.Layers[(int)slot.Value] = new( reference, st );
			if ( optimized && name == "_texcoord" ) parameters = parameters with { UvTransform = st };
		} );

		var bias = Vector3.Zero;
		var smoothness = Vector3.Zero;
		reader.Position = start;
		properties.SeekField( reader, "m_Colors" ).VisitArray( reader, (_, _) =>
		{
			var name = reader.ReadAlignedString();
			var color = new Color( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
			if ( name == "_Color" ) material.Tint = color;
			if ( !optimized ) return;

			var values = new Vector3( color.r, color.g, color.b );
			switch ( name )
			{
				case "_UV": parameters = parameters with { Tiling = values }; break;
				case "_Albedo": parameters = parameters with { Albedo = values }; break;
				case "_VertexColorPower": parameters = parameters with { VertexPower = values }; break;
				case "_Bias": bias = values; break;
				case "_Smoothness": smoothness = values; break;
			}
		} );

		if ( optimized )
		{
			material.Optimized = parameters;
			material.Bias = bias;
			material.Smoothness = smoothness;
			return material;
		}

		reader.Position = start;
		properties.SeekField( reader, "m_Floats" ).VisitArray( reader, (_, _) =>
		{
			var name = reader.ReadAlignedString();
			var value = reader.ReadSingle();
			if ( !float.IsFinite( value ) ) return;
			switch ( name )
			{
				case "_TextureFactor": material.Frequency = value; break;
				case "_Depth": material.BlendDepth = MathF.Max( 0, value ); break;
				case "_NormalStrength": material.NormalStrength = value; break;
				case "_Cull": material.Cull = (int)Math.Clamp( value, 0, 2 ); break;
				case "_RedChannelBias": bias.x = value; break;
				case "_GreenChannelBias": bias.y = value; break;
				case "_BlueChannelBias": bias.z = value; break;
				case "_RedChannelSmoothness": smoothness.x = value; break;
				case "_GreenChannelSmoothness": smoothness.y = value; break;
				case "_BlueChannelSmoothness": smoothness.z = value; break;
			}
		} );
		material.Bias = bias;
		material.Smoothness = smoothness;
		return material;
	}
}

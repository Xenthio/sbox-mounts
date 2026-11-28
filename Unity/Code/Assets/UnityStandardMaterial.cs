namespace UnityMount;

sealed class UnityStandardMaterial
{
	public enum Map { Green, Blue, GreenNormal, BlueNormal, FlowMap, FlowHeight, FlowRamp, Splat, SliceAlpha, PrintRamp, Count }
	public readonly record struct PrintSettings( float SliceHeight, float BandHeight, float AlphaDepth, float Direction,
		float Bias, float Boost, float EmissionToAlbedo, Vector4 AlphaTransform );
	public UnityReference[] Layers { get; } = new UnityReference[(int)Map.Count];
	public UnityReference Mask { get; private set; }
	public UnityReference Ramp { get; private set; }
	public Color EmissionTint { get; private set; } = Color.Black;
	public Vector4 BaseTransform { get; private set; } = new( 1, 1, 0, 0 );
	public Vector4 NormalTransform { get; private set; } = new( 1, 1, 0, 0 );
	public Vector4 SplatTransform { get; private set; } = new( 1, 1, 0, 0 );
	public float EmissionPower { get; private set; } = 1;
	public float Power { get; private set; } = 1;
	public float Boost { get; private set; } = 1;
	public float NormalStrength { get; private set; } = 1;
	public float Smoothness { get; private set; }
	public float Fade { get; private set; } = 1;
	public float FadeBias { get; private set; }
	public bool Cutout { get; private set; }
	public bool Fresnel { get; private set; }
	public bool Dither { get; private set; }
	public bool Flow { get; private set; }
	public bool Splat { get; private set; }
	public bool VertexColors { get; private set; }
	public PrintSettings? Print { get; private set; }
	public Vector4 FlowSettings { get; private set; } = new( 1, 1, 1, 0 );
	public Vector3 HeightSettings { get; private set; } = new( 0, 1, 1 );
	public Vector4 SplatSettings { get; private set; } = new( 1, 0.2f, 0, 0 );
	public Vector2 SplatSmoothness { get; private set; }
	public int Cull { get; private set; } = 2;

	public static bool Supports( string shader, string[] keywords ) =>
		shader == "Hopoo Games/Deferred/Standard" &&
		keywords.All( key => key is "FRESNEL_EMISSION" or "CUTOUT" or "DITHER" or "FORCE_SPEC" or "USE_VERTEX_COLORS" or "FLOWMAP" or "SPLATMAP" or "PRINT_CUTOFF" or "LIMBREMOVAL" );

	public static UnityStandardMaterial Read( UnityReader reader, UnityTypeTree properties, long start, string[] keywords )
	{
		var material = new UnityStandardMaterial
		{
			Cutout = keywords.Contains( "CUTOUT" ),
			Fresnel = keywords.Contains( "FRESNEL_EMISSION" ),
			Dither = keywords.Contains( "DITHER" ),
			Flow = keywords.Contains( "FLOWMAP" ),
			Splat = keywords.Contains( "SPLATMAP" ),
			VertexColors = keywords.Contains( "USE_VERTEX_COLORS" )
		};
		var flow = material.FlowSettings;
		var height = material.HeightSettings;
		var splat = material.SplatSettings;
		var smoothness = material.SplatSmoothness;
		var print = new PrintSettings( 5, 1, .1f, 0, 0, 1, 0, new( 1, 1, 0, 0 ) );
		var limbPrimeMask = 1f;
		reader.Position = start;
		properties.SeekField( reader, "m_TexEnvs" ).VisitArray( reader, (_, _) =>
		{
			var name = reader.ReadAlignedString();
			var reference = UnityReference.Read( reader );
			var transform = new Vector4( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
			switch ( name )
			{
				case "_FresnelMask": material.Mask = reference; break;
				case "_FresnelRamp": material.Ramp = reference; break;
				case "_MainTex": material.BaseTransform = transform; break;
				case "_NormalTex": material.NormalTransform = transform; break;
				case "_SplatmapTex":
					material.Layers[(int)Map.Splat] = reference;
					material.SplatTransform = transform;
					break;
				case "_GreenChannelTex": material.Layers[(int)Map.Green] = reference; break;
				case "_BlueChannelTex": material.Layers[(int)Map.Blue] = reference; break;
				case "_GreenChannelNormalTex": material.Layers[(int)Map.GreenNormal] = reference; break;
				case "_BlueChannelNormalTex": material.Layers[(int)Map.BlueNormal] = reference; break;
				case "_FlowTex": material.Layers[(int)Map.FlowMap] = reference; break;
				case "_FlowHeightmap": material.Layers[(int)Map.FlowHeight] = reference; break;
				case "_FlowHeightRamp": material.Layers[(int)Map.FlowRamp] = reference; break;
				case "_SliceAlphaTex":
					material.Layers[(int)Map.SliceAlpha] = reference;
					print = print with { AlphaTransform = transform };
					break;
				case "_PrintRamp": material.Layers[(int)Map.PrintRamp] = reference; break;
			}
		} );

		reader.Position = start;
		properties.SeekField( reader, "m_Colors" ).VisitArray( reader, (_, _) =>
		{
			var name = reader.ReadAlignedString();
			var color = new Color( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
			if ( name == "_EmColor" ) material.EmissionTint = color;
		} );

		reader.Position = start;
		properties.SeekField( reader, "m_Floats" ).VisitArray( reader, (_, _) =>
		{
			var name = reader.ReadAlignedString();
			var value = reader.ReadSingle();
			if ( name == "_LimbPrimeMask" ) limbPrimeMask = value;
			if ( !float.IsFinite( value ) ) return;
			switch ( name )
			{
				case "_EmPower": material.EmissionPower = value; break;
				case "_FresnelPower": material.Power = value; break;
				case "_FresnelBoost": material.Boost = value; break;
				case "_NormalStrength": material.NormalStrength = value; break;
				case "_Smoothness": material.Smoothness = value; break;
				case "_Fade" when material.Dither: material.Fade = value; break;
				case "_FadeBias" when material.Dither: material.FadeBias = value; break;
				case "_FlowTextureScaleFactor": flow.x = value; break;
				case "_FlowNormalStrength": flow.y = value; break;
				case "_FlowSpeed": flow.z = value; break;
				case "_FlowMaskStrength": flow.w = value; break;
				case "_FlowHeightBias": height.x = value; break;
				case "_FlowHeightPower": height.y = value; break;
				case "_FlowEmissionStrength": height.z = value; break;
				case "_SplatmapTileScale": splat.x = value; break;
				case "_Depth": splat.y = value; break;
				case "_GreenChannelBias": splat.z = value; break;
				case "_BlueChannelBias": splat.w = value; break;
				case "_GreenChannelSmoothness": smoothness.x = value; break;
				case "_BlueChannelSmoothness": smoothness.y = value; break;
				case "_SliceHeight": print = print with { SliceHeight = value }; break;
				case "_SliceBandHeight": print = print with { BandHeight = value }; break;
				case "_SliceAlphaDepth": print = print with { AlphaDepth = value }; break;
				case "_PrintDirection": print = print with { Direction = value }; break;
				case "_PrintBias": print = print with { Bias = value }; break;
				case "_PrintBoost": print = print with { Boost = value }; break;
				case "_PrintEmissionToAlbedoLerp": print = print with { EmissionToAlbedo = value }; break;
				case "_Cull": material.Cull = (int)Math.Clamp( value, 0, 2 ); break;
			}
		} );
		// A mask of one leaves all limbs visible in the serialized material.
		if ( keywords.Contains( "LIMBREMOVAL" ) && limbPrimeMask != 1f ) return null;
		material.FlowSettings = flow;
		material.HeightSettings = height;
		material.SplatSettings = splat;
		material.SplatSmoothness = smoothness;
		if ( keywords.Contains( "PRINT_CUTOFF" ) ) material.Print = print;
		return material;
	}
}

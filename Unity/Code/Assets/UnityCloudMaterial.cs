namespace UnityMount;

sealed class UnityCloudMaterial
{
	const string ShaderName = "Hopoo Games/FX/Cloud Remap";
	const string IntersectionShader = "Hopoo Games/FX/Cloud Intersection Remap";
	const string OptimizedShader = "Hopoo Games/Optimized/Switch/FX/CloudRemap";
	const string SoftAdditiveShader = "Legacy Shaders/Particles/Additive (Soft)";

	public enum CloudVariant { Standard, Intersection, Optimized, SoftAdditive }
	public static bool Supports( string shader ) => shader is ShaderName or IntersectionShader or OptimizedShader or SoftAdditiveShader;

	public readonly record struct Layer( UnityReference Reference, Vector4 Transform );
	static readonly Layer DefaultLayer = new( default, new Vector4( 1, 1, 0, 0 ) );

	public Layer Main { get; private set; } = DefaultLayer;
	public Layer First { get; private set; } = DefaultLayer;
	public Layer Second { get; private set; } = DefaultLayer;
	public UnityReference Remap { get; private set; }
	public Color Tint { get; private set; } = Color.White;
	public Vector4 Scroll { get; private set; }
	public float Boost { get; private set; } = 1;
	public float AlphaBoost { get; private set; } = 1;
	public float AlphaBias { get; private set; }
	public float ExternalAlpha { get; private set; } = 1;
	public float Fade { get; private set; } = 1;
	public float Distortion { get; private set; } = 0.1f;
	public float InverseFade { get; private set; } = 0.1f;
	public float CloseFade { get; private set; }
	public float DepthOffset { get; private set; }
	public float VertexOffset { get; private set; }
	public float FresnelPower { get; private set; }
	public int Cull { get; private set; }
	public bool Additive { get; private set; }
	public bool Clouds { get; private set; }
	public bool Distort { get; private set; }
	public bool VertexColor { get; private set; }
	public bool VertexAlpha { get; private set; }
	public bool DisableRemap { get; private set; }
	public bool SkyOnly { get; private set; }
	public bool UseUv1 { get; private set; }
	public bool AlwaysDraw { get; private set; }
	public CloudVariant Variant { get; private set; }
	public bool Triplanar { get; private set; }
	public Vector4 IntersectionStyle { get; private set; } = new( 1, 1, 1, 0 );
	public Vector4 DistortedMainTransform { get; private set; } = DefaultLayer.Transform;
	public Vector4 TexCoordTransform { get; private set; } = DefaultLayer.Transform;
	public float RemapFactor { get; private set; }

	public static UnityCloudMaterial Read( UnityReader reader, UnityTypeTree properties, long start, string[] keywords, string shader )
	{
		var intersection = shader == IntersectionShader;
		var optimized = shader == OptimizedShader;
		var softAdditive = shader == SoftAdditiveShader;
		var material = new UnityCloudMaterial
		{
			Variant = shader switch
			{
				IntersectionShader => CloudVariant.Intersection,
				OptimizedShader => CloudVariant.Optimized,
				SoftAdditiveShader => CloudVariant.SoftAdditive,
				_ => CloudVariant.Standard
			},
			Tint = optimized ? Color.Black : Color.White,
			AlphaBias = optimized ? 0.2f : 0,
			Distortion = optimized ? 0 : 0.1f,
			Triplanar = intersection && keywords.Contains( "TRIPLANAR" ),
			InverseFade = intersection || softAdditive ? 1f : 0.1f,
			Clouds = keywords.Contains( optimized ? "_CLOUDSON_ON" : "USE_CLOUDS" ),
			Distort = keywords.Contains( "CLOUDOFFSET" ),
			VertexColor = keywords.Contains( optimized ? "_VertexColorOn" : "VERTEXCOLOR" ),
			VertexAlpha = keywords.Contains( intersection ? "FADE_FROM_VERTEX_COLORS" : "VERTEXALPHA" ),
			DisableRemap = keywords.Contains( optimized ? "_DISABLEREMAPON_ON" : "DISABLEREMAP" ),
			SkyOnly = keywords.Contains( "SKYBOX_ONLY" ),
			UseUv1 = keywords.Contains( "USE_UV1" )
		};
		reader.Position = start;
		properties.SeekField( reader, "m_TexEnvs" ).VisitArray( reader, (_, _) =>
		{
			var name = reader.ReadAlignedString();
			var reference = UnityReference.Read( reader );
			var transform = new Vector4( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
			var layer = new Layer( reference, transform );
			switch ( name )
			{
				case "_MainTex": material.Main = layer; break;
				case "_Cloud1Tex": material.First = layer; break;
				case "_Cloud2Tex": material.Second = layer; break;
				case "_RemapTex": material.Remap = reference; break;
				case "_texcoord" when optimized: material.TexCoordTransform = transform; break;
			}
		} );
		var distortedMain = DefaultLayer.Transform;
		var cloudTiling = DefaultLayer.Transform;
		reader.Position = start;
		properties.SeekField( reader, "m_Colors" ).VisitArray( reader, (_, _) =>
		{
			var name = reader.ReadAlignedString();
			var value = new Vector4( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
			if ( name == "_TintColor" ) material.Tint = new Color( value.x, value.y, value.z, value.w );
			if ( name == "_CutoffScroll" ) material.Scroll = value;
			if ( optimized )
			{
				if ( name == "_Tiling" ) (distortedMain.x, distortedMain.y) = (value.x, value.y);
				if ( name == "_Offset" ) (distortedMain.z, distortedMain.w) = (value.x, value.y);
				if ( name == "_CloudTiling" ) cloudTiling = new( value.x, value.y, 0, 0 );
			}
		} );
		var sourceBlend = optimized ? 5f : 1f;
		var destinationBlend = optimized ? 10f : 1f;
		var intersectionStyle = material.IntersectionStyle;
		var closeFade = keywords.Contains( "FADECLOSE" );
		var fresnel = keywords.Contains( "FRESNEL" );
		var vertexOffset = keywords.Contains( "VERTEXOFFSET" );
		reader.Position = start;
		properties.SeekField( reader, "m_Floats" ).VisitArray( reader, (_, _) =>
		{
			var name = reader.ReadAlignedString();
			var value = reader.ReadSingle();
			if ( !float.IsFinite( value ) ) return;
			switch ( name )
			{
				case "_SrcBlend" when !intersection && !optimized:
				case "_SrcBlendFloat" when intersection: sourceBlend = value; break;
				case "_DstBlend" when !intersection && !optimized:
				case "_DstBlendFloat" when intersection: destinationBlend = value; break;
				case "_Boost": material.Boost = value; break;
				case "_AlphaBoost": material.AlphaBoost = value; break;
				case "_AlphaBias": material.AlphaBias = value; break;
				case "_ExternalAlpha": material.ExternalAlpha = value; break;
				case "_Fade" when !intersection: material.Fade = value; break;
				case "_DistortionStrength": material.Distortion = value; break;
				case "_InvFade": material.InverseFade = value; break;
				case "_FadeCloseDistance" when closeFade: material.CloseFade = value; break;
				case "_DepthOffset": material.DepthOffset = value; break;
				case "_OffsetAmount" when vertexOffset: material.VertexOffset = value; break;
				case "_FresnelPower" when fresnel: material.FresnelPower = value; break;
				case "_Cull" when !optimized: material.Cull = (int)Math.Clamp( value, 0, 2 ); break;
				case "_ZTest" when !optimized: material.AlwaysDraw = value == 8; break;
				case "_RemapAdd" when optimized: material.RemapFactor = value; break;
				case "_SoftPower" when intersection: intersectionStyle.x = value; break;
				case "_RimPower" when intersection: intersectionStyle.y = value; break;
				case "_RimStrength" when intersection: intersectionStyle.z = value; break;
				case "_IntersectionStrength" when intersection: intersectionStyle.w = value; break;
			}
		} );
		// Unity's built-in particle shader has fixed render states.
		if ( softAdditive ) return material;
		if ( (sourceBlend, destinationBlend) is not ((1, 1) or (5, 10)) ) return null;
		material.Additive = sourceBlend == 1;
		material.IntersectionStyle = intersectionStyle;
		if ( optimized )
		{
			material.DistortedMainTransform = distortedMain;
			material.First = material.First with { Transform = cloudTiling };
		}
		return material;
	}
}

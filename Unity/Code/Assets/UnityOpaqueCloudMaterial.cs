using Layer = UnityMount.UnityCloudMaterial.Layer;

namespace UnityMount;

sealed class UnityOpaqueCloudMaterial
{
	const string StandardShader = "Hopoo Games/FX/Opaque Cloud Remap";
	const string OptimizedShader = "Hopoo Games/Optimized/Switch/FX/OpaqueCloudRemap";
	static readonly Layer DefaultLayer = new( default, new Vector4( 1, 1, 0, 0 ) );

	public static bool Supports( string shader ) => shader is StandardShader or OptimizedShader;

	public Layer Main { get; private set; } = DefaultLayer;
	public Layer First { get; private set; } = DefaultLayer;
	public Layer Second { get; private set; } = DefaultLayer;
	public UnityReference Normal { get; private set; }
	public UnityReference Remap { get; private set; }
	public Color Tint { get; private set; } = Color.White;
	public Color Emission { get; private set; } = Color.White;
	public Color FresnelTint { get; private set; } = Color.White;
	public Vector4 Scroll { get; private set; }
	public Vector4 DistortedMain { get; private set; } = DefaultLayer.Transform;
	public float Cutoff { get; private set; } = .5f;
	public float AlphaBoost { get; private set; } = 1;
	public float AlphaBias { get; private set; } = .2f;
	public float ExternalAlpha { get; private set; } = 1;
	public float Boost { get; private set; } = 1;
	public float NormalStrength { get; private set; } = 1;
	public float VertexInfluence { get; private set; } = 1;
	public float Distortion { get; private set; }
	public float RemapFactor { get; private set; }
	public float FresnelPower { get; private set; }
	public int Cull { get; private set; } = 2;
	public bool Optimized { get; private set; }
	public bool Clouds { get; private set; }
	public bool CloudNormal { get; private set; }
	public bool EmissionFromAlbedo { get; private set; }
	public bool VertexAlpha { get; private set; }
	public bool VertexColor { get; private set; }
	public bool DisableRemap { get; private set; }

	public static UnityOpaqueCloudMaterial Read( UnityReader reader, UnityTypeTree properties, long start, string[] keywords, string shader )
	{
		var optimized = shader == OptimizedShader;
		var material = new UnityOpaqueCloudMaterial
		{
			Optimized = optimized,
			Cutoff = optimized ? .164f : .5f,
			Cull = optimized ? 0 : 2,
			Tint = optimized ? Color.Black : Color.White,
			Clouds = keywords.Contains( "_CLOUDSON_ON" ),
			CloudNormal = keywords.Contains( "CLOUDNORMAL" ),
			EmissionFromAlbedo = keywords.Contains( "EMISSIONFROMALBEDO" ),
			VertexAlpha = keywords.Contains( "VERTEXALPHA" ),
			VertexColor = keywords.Contains( "_VertexColorOn" ),
			DisableRemap = keywords.Contains( "_DISABLEREMAPON_ON" )
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
				case "_NormalTex": material.Normal = reference; break;
				case "_RemapTex": material.Remap = reference; break;
				case "_texcoord" when optimized: material.DistortedMain = transform; break;
			}
		} );

		var tiling = DefaultLayer.Transform;
		var cloudTiling = DefaultLayer.Transform;
		reader.Position = start;
		properties.SeekField( reader, "m_Colors" ).VisitArray( reader, (_, _) =>
		{
			var name = reader.ReadAlignedString();
			var color = UnityEffectValues.ReadColor( reader );
			switch ( name )
			{
				case "_TintColor": material.Tint = color; break;
				case "_EmissionColor": material.Emission = color; break;
				case "_FresnelTintColor": material.FresnelTint = color; break;
				case "_CutoffScroll": material.Scroll = new Vector4( color.r, color.g, color.b, color.a ); break;
				case "_Tiling" when optimized: (tiling.x, tiling.y) = (color.r, color.g); break;
				case "_Offset" when optimized: (tiling.z, tiling.w) = (color.r, color.g); break;
				case "_CloudTiling" when optimized: (cloudTiling.x, cloudTiling.y) = (color.r, color.g); break;
			}
		} );
		if ( optimized )
		{
			// The optimized shader samples the same texture once for the base mask and again at a distorted UV.
			(material.Main, material.DistortedMain) = (material.Main with { Transform = material.DistortedMain }, tiling);
			material.First = material.First with { Transform = cloudTiling };
		}

		var fresnel = keywords.Contains( "FRESNEL" );
		reader.Position = start;
		properties.SeekField( reader, "m_Floats" ).VisitArray( reader, (_, _) =>
		{
			var name = reader.ReadAlignedString();
			var value = reader.ReadSingle();
			if ( !float.IsFinite( value ) ) return;
			switch ( name )
			{
				case "_Cutoff": material.Cutoff = Math.Clamp( value, 0, 1 ); break;
				case "_AlphaBoost": material.AlphaBoost = value; break;
				case "_AlphaBias": material.AlphaBias = value; break;
				case "_ExternalAlpha": material.ExternalAlpha = value; break;
				case "_Boost": material.Boost = value; break;
				case "_NormalStrength": material.NormalStrength = value; break;
				case "_VertexColorInfluence": material.VertexInfluence = value; break;
				case "_DistortionStrength": material.Distortion = value; break;
				case "_RemapAdd": material.RemapFactor = value; break;
				case "_FresnelPower" when fresnel: material.FresnelPower = value; break;
				case "_Cull" when !optimized: material.Cull = (int)Math.Clamp( value, 0, 2 ); break;
			}
		} );
		return material;
	}
}

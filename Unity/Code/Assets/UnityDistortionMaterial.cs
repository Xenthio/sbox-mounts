namespace UnityMount;

sealed class UnityDistortionMaterial
{
	public const string ShaderName = "Hopoo Games/FX/Distortion";

	public readonly record struct Layer( UnityReference Reference, Vector4 Transform );
	static readonly Layer DefaultLayer = new( default, new Vector4( 1, 1, 0, 0 ) );

	public Layer Normal { get; private set; } = DefaultLayer;
	public Layer Mask { get; private set; } = DefaultLayer;
	public float Magnitude { get; private set; } = 0.05f;
	public Vector4 CameraFade { get; private set; } = new( 0, 5, 250000, 200000 );
	public float InverseFade { get; private set; } = 0.1f;
	public float DistanceModulation { get; private set; }

	public static UnityDistortionMaterial Read( UnityReader reader, UnityTypeTree properties, long start, string[] keywords )
	{
		var material = new UnityDistortionMaterial();
		reader.Position = start;
		properties.SeekField( reader, "m_TexEnvs" ).VisitArray( reader, (_, _) =>
		{
			var name = reader.ReadAlignedString();
			var reference = UnityReference.Read( reader );
			var transform = new Vector4( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
			if ( name == "_BumpMap" ) material.Normal = new( reference, transform );
			if ( name == "_MaskTex" ) material.Mask = new( reference, transform );
		} );

		var fade = material.CameraFade;
		var modulation = 0.5f;
		reader.Position = start;
		properties.SeekField( reader, "m_Floats" ).VisitArray( reader, (_, _) =>
		{
			var name = reader.ReadAlignedString();
			var value = reader.ReadSingle();
			if ( !float.IsFinite( value ) ) return;
			switch ( name )
			{
				case "_Magnitude": material.Magnitude = value; break;
				case "_NearFadeZeroDistance": fade.x = value; break;
				case "_NearFadeOneDistance": fade.y = value; break;
				case "_FarFadeZeroDistance": fade.z = value; break;
				case "_FarFadeOneDistance": fade.w = value; break;
				case "_InvFade": material.InverseFade = value; break;
				case "_DistanceModulationMagnitude": modulation = value; break;
			}
		} );
		material.CameraFade = fade;
		material.DistanceModulation = keywords.Contains( "DISTANCEMODULATION" ) ? modulation : 0f;
		return material;
	}
}

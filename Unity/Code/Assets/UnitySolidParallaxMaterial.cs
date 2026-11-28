namespace UnityMount;

sealed class UnitySolidParallaxMaterial
{
	public enum Map { Color, Emission, Normal, Height1, Height2, Count }
	public readonly record struct Layer( UnityReference Reference, Vector4 Transform );
	public Layer[] Layers { get; } = Enumerable.Repeat( new Layer( default, new( 1, 1, 0, 0 ) ), (int)Map.Count ).ToArray();
	public Color Tint { get; private set; } = Color.White;
	public Vector4 Scroll { get; private set; }
	public float HeightStrength { get; private set; } = 1f;
	public float HeightBias { get; private set; }
	public float Parallax { get; private set; }
	public float EmissionPower { get; private set; } = 1f;
	public float Smoothness { get; private set; }
	public int Cull { get; private set; } = 2;
	public bool AlphaClip { get; private set; }

	public static bool Supports( string shader, string[] keywords ) =>
		shader == "Hopoo Games/FX/Solid Parallax" && keywords.All( key => key == "ALPHACLIP" );

	public static UnitySolidParallaxMaterial Read( UnityReader reader, UnityTypeTree properties, long start, string[] keywords )
	{
		var material = new UnitySolidParallaxMaterial { AlphaClip = keywords.Contains( "ALPHACLIP" ) };
		reader.Position = start;
		properties.SeekField( reader, "m_TexEnvs" ).VisitArray( reader, (_, _) =>
		{
			var name = reader.ReadAlignedString();
			var reference = UnityReference.Read( reader );
			var transform = new Vector4( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
			Map? slot = name switch
			{
				"_MainTex" => Map.Color,
				"_EmissionTex" => Map.Emission,
				"_Normal" => Map.Normal,
				"_Height1" => Map.Height1,
				"_Height2" => Map.Height2,
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
			if ( name == "_ScrollSpeed" ) material.Scroll = new( color.r, color.g, color.b, color.a );
		} );

		reader.Position = start;
		properties.SeekField( reader, "m_Floats" ).VisitArray( reader, (_, _) =>
		{
			var name = reader.ReadAlignedString();
			var value = reader.ReadSingle();
			if ( !float.IsFinite( value ) ) return;
			switch ( name )
			{
				case "_HeightStrength": material.HeightStrength = value; break;
				case "_HeightBias": material.HeightBias = value; break;
				case "_Parallax": material.Parallax = value; break;
				case "_EmissionPower": material.EmissionPower = value; break;
				case "_Smoothness": material.Smoothness = value; break;
				case "_Cull": material.Cull = (int)Math.Clamp( value, 0, 2 ); break;
			}
		} );
		return material;
	}
}

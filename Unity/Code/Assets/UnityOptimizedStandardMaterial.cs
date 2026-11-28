namespace UnityMount;

sealed class UnityOptimizedStandardMaterial
{
	public enum Map { Color, Normal, Emission, Ramp, Splat, Green, Blue, Flow, FlowNormal, FlowHeight, FlowRamp, Count }
	public readonly record struct Layer( UnityReference Reference, Vector4 Transform );
	public Layer[] Layers { get; } = Enumerable.Repeat( new Layer( default, new( 1, 1, 0, 0 ) ), (int)Map.Count ).ToArray();
	public Vector3 Tint { get; private set; } = Vector3.One;
	public Vector3 EmissionTint { get; private set; }
	public Vector2 NormalTiling { get; private set; } = Vector2.One;
	public float NormalStrength { get; private set; } = 1f;
	public float AlbedoMultiplier { get; private set; } = 2.6f;
	public float Smoothness { get; private set; }
	public Vector4 FresnelSettings { get; private set; } = new( 1, 5, 0, 0 );
	public Vector4 SplatSettings { get; private set; }
	public Vector4 FlowSettings { get; private set; } = new( 3, 0, 0, 1 );
	public float FlowEmission { get; private set; } = 1f;
	public bool Fresnel { get; private init; }
	public bool Splat { get; private init; }
	public bool VertexSplat { get; private init; }
	public bool Flow { get; private init; }

	// The optimized deferred variants use the same base normal with or without _SPLATMAPNORMALON_ON.
	public static bool Supports( string shader, string[] keywords ) =>
		shader == "Hopoo Games/Optimized/Switch/Environment/Standard" &&
		keywords.All( key => key is "_FEON_ON" or "_SPLATMAPON_ON" or "_COLORSON_ON" or "_SPLATMAPNORMALON_ON" or "_FLOWMAPON_ON" );

	public static UnityOptimizedStandardMaterial Read( UnityReader reader, UnityTypeTree properties, long start, string[] keywords )
	{
		var material = new UnityOptimizedStandardMaterial
		{
			Fresnel = keywords.Contains( "_FEON_ON" ),
			Splat = keywords.Contains( "_SPLATMAPON_ON" ),
			VertexSplat = keywords.Contains( "_COLORSON_ON" ),
			Flow = keywords.Contains( "_FLOWMAPON_ON" )
		};

		reader.Position = start;
		properties.SeekField( reader, "m_TexEnvs" ).VisitArray( reader, (_, _) =>
		{
			var name = reader.ReadAlignedString();
			var reference = UnityReference.Read( reader );
			var transform = new Vector4( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
			Map? slot = name switch
			{
				"_MainTex" => Map.Color,
				"_NormalTex" => Map.Normal,
				"_EmTex" => Map.Emission,
				"_FresnelRamp" when material.Fresnel => Map.Ramp,
				"_SplatmapTex" when material.Splat && !material.VertexSplat => Map.Splat,
				"_GreenChannelTex" when material.Splat => Map.Green,
				"_BlueChannelTex" when material.Splat => Map.Blue,
				"_FlowTex" when material.Flow => Map.Flow,
				"_FlowNormal" when material.Flow => Map.FlowNormal,
				"_FlowHeightmap" when material.Flow => Map.FlowHeight,
				"_FlowHeightRamp" when material.Flow => Map.FlowRamp,
				_ => null
			};
			if ( slot is { } layer ) material.Layers[(int)layer] = new( reference, transform );
		} );

		reader.Position = start;
		properties.SeekField( reader, "m_Colors" ).VisitArray( reader, (_, _) =>
		{
			var name = reader.ReadAlignedString();
			var color = new Vector3( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
			reader.ReadSingle();
			switch ( name )
			{
				case "_Color": material.Tint = color; break;
				case "_EmColor": material.EmissionTint = color; break;
				case "_NormalTiling": material.NormalTiling = new( color.x, color.y ); break;
			}
		} );

		var fresnel = material.FresnelSettings;
		var splat = material.SplatSettings;
		var flow = material.FlowSettings;
		reader.Position = start;
		properties.SeekField( reader, "m_Floats" ).VisitArray( reader, (_, _) =>
		{
			var name = reader.ReadAlignedString();
			var value = reader.ReadSingle();
			if ( !float.IsFinite( value ) ) return;
			switch ( name )
			{
				case "_NormalStrength": material.NormalStrength = value; break;
				case "_AlbedoMult": material.AlbedoMultiplier = value; break;
				case "_Smoothness": material.Smoothness = value; break;
				case "_FresnelScale": fresnel.x = value; break;
				case "_FresnelPower": fresnel.y = value; break;
				case "_FresnelAphaAdd": fresnel.z = value; break;
				case "_FresnelAdd": fresnel.w = value; break;
				case "_SplatmapTileScale": splat.x = value; break;
				case "_GreenChannelBias": splat.y = value; break;
				case "_BlueChannelBias": splat.z = value; break;
				case "_Depth": splat.w = value; break;
				case "_FlowTextureScaleFactor": flow.x = value; break;
				case "_FlowSpeed": flow.y = value; break;
				case "_FlowHeightBias": flow.z = value; break;
				case "_FlowHeightPower": flow.w = value; break;
				case "_FlowEmissionStrength": material.FlowEmission = value; break;
			}
		} );
		material.FresnelSettings = fresnel;
		material.SplatSettings = splat;
		material.FlowSettings = flow;
		return material;
	}
}

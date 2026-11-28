namespace UnityMount;

sealed record UnityMaterial( string Name, UnityReference Diffuse, UnityReference Normal, UnityReference Emission,
	Color Tint, Vector2 Scale, Vector2 Offset, Color EmissionTint, float EmissionPower, float? AlphaCutoff, bool DoubleSided,
	UnityCloudMaterial Cloud, UnityOpaqueCloudMaterial OpaqueCloud, UnityTerrainMaterial Terrain, UnitySnowMaterial Snow,
	UnityStandardMaterial Standard, UnityOptimizedStandardMaterial OptimizedStandard, UnitySolidParallaxMaterial SolidParallax,
	UnityWavyClothMaterial WavyCloth, UnityDistortionMaterial Distortion, bool SimpleOpaque, bool LegacyAdditiveParticle )
{
	public const int ClassId = 21;

	public static UnityMaterial Read( UnityReader reader, UnityTypeTree tree, Func<UnityReference, UnityShader> readShader = null,
		bool identifyShader = false )
	{
		tree.SeekField( reader, "m_Name" );
		var name = reader.ReadAlignedString();
		reader.Position = 0;
		var saved = tree.SeekField( reader, "m_SavedProperties" );
		var properties = reader.Position;
		saved.SeekField( reader, "m_TexEnvs" );
		var count = reader.ReadInt32();
		if ( count < 0 || count > reader.Remaining / 32 ) throw new InvalidDataException( "Invalid Unity material texture count." );

		UnityReference diffuse = default, normal = default, emission = default;
		var scale = Vector2.One;
		var offset = Vector2.Zero;
		var usesBaseMap = false;
		var usesRoR2Emission = false;
		var hasCloudTextures = false;
		var hasTerrainTextures = false;
		var hasSnowTextures = false;
		var hasStandardLayers = false;
		var hasParallaxTextures = false;
		var hasScrollingNormal = false;
		for ( var i = 0; i < count; i++ )
		{
			var key = reader.ReadAlignedString();
			var texture = UnityReference.Read( reader );
			var tiling = new Vector2( reader.ReadSingle(), reader.ReadSingle() );
			var shift = new Vector2( reader.ReadSingle(), reader.ReadSingle() );
			hasCloudTextures |= key is "_Cloud1Tex" or "_RemapTex";
			hasTerrainTextures |= key is "_RedChannelTopTex" or "_RedChannelSideTex";
			hasSnowTextures |= key == "_SnowTex";
			hasStandardLayers |= key is "_FlowTex" or "_GreenChannelTex" or "_BlueChannelTex" or "_SplatmapTex";
			hasParallaxTextures |= key is "_Height1" or "_Height2";
			hasScrollingNormal |= key == "_ScrollingNormalMap";
			if ( texture.Id == 0 ) continue;

			switch ( key )
			{
				case "_MainTex" when !usesBaseMap:
				case "_BaseMap":
					diffuse = texture;
					scale = tiling;
					offset = shift;
					usesBaseMap = key == "_BaseMap";
					break;
				case "_BumpMap" when normal.Id == 0:
				case "_NormalTex":
					normal = texture;
					break;
				case "_EmissionMap" when !usesRoR2Emission:
				case "_EmTex":
					emission = texture;
					usesRoR2Emission = key == "_EmTex";
					break;
			}
		}

		Color? tint = null;
		var emissionTint = Color.Black;
		reader.Position = properties;
		saved.SeekField( reader, "m_Colors" );
		count = reader.ReadInt32();
		if ( count < 0 || count > reader.Remaining / 20 ) throw new InvalidDataException( "Invalid Unity material color count." );
		for ( var i = 0; i < count; i++ )
		{
			var key = reader.ReadAlignedString();
			var color = new Color( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
			if ( key is "_Color" or "_BaseColor" ) tint = color;
			if ( key == (usesRoR2Emission ? "_EmColor" : "_EmissionColor") ) emissionTint = color;
		}

		var emissionPower = 1f;
		float? alphaCutoff = null;
		var alphaClip = false;
		var hasStandardProperties = false;
		var hasDepthFade = false;
		var doubleSided = false;
		var frontCull = false;
		reader.Position = properties;
		saved.SeekField( reader, "m_Floats" );
		count = reader.ReadInt32();
		if ( count < 0 || count > reader.Remaining / 8 ) throw new InvalidDataException( "Invalid Unity material float count." );
		for ( var i = 0; i < count; i++ )
		{
			var key = reader.ReadAlignedString();
			var value = reader.ReadSingle();
			if ( !float.IsFinite( value ) ) continue;
			switch ( key )
			{
				case "_EmPower" when usesRoR2Emission: emissionPower = value; break;
				case "_AlbedoMult":
				case "_NormalStrength": hasStandardProperties = true; break;
				case "_InvFade": hasDepthFade = true; break;
				case "_AlphaClip": alphaClip = value > 0.5f; break;
				case "_Cutoff": alphaCutoff = Math.Clamp( value, 0, 1 ); break;
				case "_Cull":
					doubleSided = value == 0;
					frontCull = value == 1;
					break;
			}
		}

		var keywords = ReadKeywords( reader, tree );
		if ( !usesRoR2Emission && !keywords.Contains( "_EMISSION" ) ) emission = default;
		// SpeedTree uses separate geometry variants for alpha-tested fronds and leaves.
		var useCutout = alphaClip || keywords.Any( key => key is "_ALPHATEST_ON" or "CUTOUT" or "GEOM_TYPE_FROND" or "GEOM_TYPE_LEAF" );
		var renderType = useCutout ? null : ReadRenderType( reader, tree );
		UnityShader shader = null;
		if ( readShader is not null && (identifyShader || hasCloudTextures || hasTerrainTextures || hasSnowTextures || hasStandardLayers ||
			hasStandardProperties || hasDepthFade || hasParallaxTextures || hasScrollingNormal ||
			keywords.Any( key => key is "FRESNEL_EMISSION" or "DITHER" or "PRINT_CUTOFF" or "LIMBREMOVAL" ) ||
			(!useCutout && renderType is null && alphaCutoff.HasValue)) &&
			tree.HasField( "m_Shader" ) )
		{
			reader.Position = 0;
			tree.SeekField( reader, "m_Shader" );
			shader = readShader( UnityReference.Read( reader ) );
		}
		useCutout |= (renderType ?? shader?.RenderType) is "TransparentCutout" or "TreeTransparentCutout" ||
			shader?.Name == "Nature/SpeedTree Billboard";
		var cloud = UnityCloudMaterial.Supports( shader?.Name ) ? UnityCloudMaterial.Read( reader, saved, properties, keywords, shader.Name ) : null;
		var opaque = UnityOpaqueCloudMaterial.Supports( shader?.Name )
			? UnityOpaqueCloudMaterial.Read( reader, saved, properties, keywords, shader.Name ) : null;
		var terrain = UnityTerrainMaterial.Supports( shader?.Name )
			? UnityTerrainMaterial.Read( reader, saved, properties, keywords, shader.Name ) : null;
		var snow = UnitySnowMaterial.Supports( shader?.Name, keywords )
			? UnitySnowMaterial.Read( reader, saved, properties, keywords ) : null;
		var standard = UnityStandardMaterial.Supports( shader?.Name, keywords )
			? UnityStandardMaterial.Read( reader, saved, properties, keywords ) : null;
		var optimizedStandard = UnityOptimizedStandardMaterial.Supports( shader?.Name, keywords )
			? UnityOptimizedStandardMaterial.Read( reader, saved, properties, keywords ) : null;
		var solidParallax = UnitySolidParallaxMaterial.Supports( shader?.Name, keywords )
			? UnitySolidParallaxMaterial.Read( reader, saved, properties, keywords ) : null;
		var wavyCloth = UnityWavyClothMaterial.Supports( shader?.Name, keywords )
			? UnityWavyClothMaterial.Read( reader, saved, properties, keywords ) : null;
		var distortion = shader?.Name == UnityDistortionMaterial.ShaderName
			? UnityDistortionMaterial.Read( reader, saved, properties, keywords ) : null;
		var simpleOpaque = shader?.Name == "Hopoo Games/Deferred/Standard" &&
			(renderType ?? shader.RenderType) == "Opaque" && keywords.Length == 0 && !useCutout && !frontCull;
		var surfaceTint = tint ?? (standard is null ? Color.White : new Color( .5f, .5f, .5f ));
		return new( name, diffuse, normal, emission, surfaceTint, scale, offset, emissionTint, emissionPower,
			useCutout ? alphaCutoff ?? 0.5f : null, doubleSided, cloud, opaque, terrain, snow, standard, optimizedStandard, solidParallax, wavyCloth, distortion, simpleOpaque,
			shader?.Name == "Legacy Shaders/Particles/Additive" );
	}

	static string ReadRenderType( UnityReader reader, UnityTypeTree tree )
	{
		if ( !tree.HasField( "stringTagMap" ) ) return null;
		reader.Position = 0;
		return UnityShader.ReadRenderTypeTag( reader, tree.SeekField( reader, "stringTagMap" ) );
	}

	static string[] ReadKeywords( UnityReader reader, UnityTypeTree tree )
	{
		if ( !tree.HasField( "m_ValidKeywords" ) ) return [];

		reader.Position = 0;
		tree.SeekField( reader, "m_ValidKeywords" );
		var count = reader.ReadInt32();
		if ( count < 0 || count > reader.Remaining / 4 ) throw new InvalidDataException( "Invalid Unity material keyword count." );
		var keywords = new string[count];
		for ( var i = 0; i < count; i++ ) keywords[i] = reader.ReadAlignedString();
		return keywords;
	}
}

namespace UnityMount;

static class RiskOfRain2LightAnimations
{
	public static void Read( BundleIndex bundles, UnityAssetFile[] files, UnityVisualOverrides visuals )
	{
		var scripts = new Dictionary<UnityAssetPointer, bool>();
		foreach ( var file in files )
		{
			foreach ( var asset in file.Data.Objects )
			{
				if ( asset.ClassId != SerializedFile.MonoBehaviourClassId || asset.Tree?.HasField( "enableNegativeLights" ) != true ) continue;
				try
				{
					using var reader = Open( file, asset );
					var tree = asset.Tree;
					var enabled = UnitySceneObjects.ReadComponentEnabled( reader, tree );

					reader.Position = 0;
					tree.SeekField( reader, "m_Script" );
					if ( !bundles.TryResolve( file, UnityReference.Read( reader ), SerializedFile.MonoScriptClassId,
						out var scriptFile, out var script ) || script.Tree is null ) continue;

					var key = new UnityAssetPointer( scriptFile, script );
					if ( !scripts.TryGetValue( key, out var matches ) )
						scripts[key] = matches = IsLightAnimation( key );
					if ( !matches ) continue;

					reader.Position = 0;
					tree.SeekField( reader, "m_GameObject" );
					if ( !bundles.TryResolve( file, UnityReference.Read( reader ), UnitySceneObjects.GameObjectClassId,
						out var ownerFile, out var owner ) ) continue;

					if ( ReadAnimation( reader, tree, enabled ) is { } animation )
						visuals.LightAnimations.TryAdd( new( ownerFile, owner ), animation );
				}
				catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
				{
					Log.Warning( $"Could not read Risk of Rain 2 light animation {asset.Id}: {e.Message}" );
				}
			}
		}
	}

	static UnityLightAnimationData? ReadAnimation( UnityReader reader, UnityTypeTree tree, bool enabled )
	{
		reader.Position = 0;
		tree.SeekField( reader, "timeMax" );
		var duration = reader.ReadSingle();
		if ( duration <= 0 || !float.IsFinite( duration ) ) return null;

		reader.Position = 0;
		tree.SeekField( reader, "loop" );
		var loop = reader.ReadBoolean();
		var randomStart = reader.ReadBoolean();
		if ( reader.ReadBoolean() ) return null; // Negative lights require a different light type.

		reader.Position = 0;
		var curve = UnityEffectValues.ReadCurve( reader, tree.SeekField( reader, "curve" ) );
		return curve.Length == 0 ? null : new( curve, duration, loop, randomStart, enabled );
	}

	static bool IsLightAnimation( UnityAssetPointer script )
	{
		using var reader = Open( script.File, script.Asset );
		var tree = script.Asset.Tree;
		tree.SeekField( reader, "m_ClassName" );
		if ( reader.ReadAlignedString() != "LightIntensityCurve" ) return false;
		reader.Position = 0;
		tree.SeekField( reader, "m_AssemblyName" );
		return reader.ReadAlignedString() == "RoR2.dll";
	}

	static UnityReader Open( UnityAssetFile file, SerializedFile.Object asset )
	{
		const int maxSize = 4 * 1024 * 1024;
		if ( asset.Size > maxSize ) throw new InvalidDataException( "Unity light animation metadata is too large." );
		return new( file.ReadObject( asset, maxSize ), leaveOpen: false );
	}
}

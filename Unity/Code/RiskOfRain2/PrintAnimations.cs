namespace UnityMount;

static class RiskOfRain2PrintAnimations
{
	public static void Read( BundleIndex bundles, UnityAssetFile[] files, UnityVisualOverrides visuals )
	{
		var scripts = new Dictionary<UnityAssetPointer, bool>();
		foreach ( var file in files )
		{
			foreach ( var asset in file.Data.Objects )
			{
				var tree = asset.Tree;
				if ( asset.ClassId != SerializedFile.MonoBehaviourClassId || tree?.HasField( "startingPrintHeight" ) != true ) continue;
				try
				{
					using var reader = Open( file, asset );
					tree.SeekField( reader, "m_Script" );
					if ( !bundles.TryResolve( file, UnityReference.Read( reader ), SerializedFile.MonoScriptClassId,
						out var scriptFile, out var script ) || script.Tree is null ) continue;
					var key = new UnityAssetPointer( scriptFile, script );
					if ( !scripts.TryGetValue( key, out var matches ) )
						scripts[key] = matches = IsPrintController( key );
					if ( !matches ) continue;

					reader.Position = 0;
					tree.SeekField( reader, "m_GameObject" );
					if ( bundles.TryResolve( file, UnityReference.Read( reader ), UnitySceneObjects.GameObjectClassId, out var ownerFile, out var owner ) &&
						ReadAnimation( reader, tree ) is { } animation )
						visuals.PrintAnimations.TryAdd( new( ownerFile, owner ), animation );
				}
				catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
				{
					Log.Warning( $"Could not read Risk of Rain 2 print animation {asset.Id}: {e.Message}" );
				}
			}
		}
	}

	static UnityPrintAnimationData? ReadAnimation( UnityReader reader, UnityTypeTree tree )
	{
		var enabled = UnitySceneObjects.ReadComponentEnabled( reader, tree );
		var duration = ReadFloat( "printTime" );
		if ( duration <= 0 || !float.IsFinite( duration ) ) return null;
		reader.Position = 0;
		var curve = UnityEffectValues.ReadCurve( reader, tree.SeekField( reader, "printCurve" ) );
		if ( curve.Length == 0 ) return null;
		var height = new Vector2( ReadFloat( "startingPrintHeight" ), ReadFloat( "maxPrintHeight" ) );
		var bias = new Vector2( ReadFloat( "startingPrintBias" ), ReadFloat( "maxPrintBias" ) );
		Vector2? flow = ReadBool( "animateFlowmapPower" ) ? new( ReadFloat( "startingFlowmapPower" ), ReadFloat( "maxFlowmapPower" ) ) : null;
		return new( curve, duration, height, bias, flow, enabled, ReadBool( "paused" ), ReadBool( "disableWhenFinished" ),
			ReadBool( "ignoreMaterialsWithPrintControllers" ) );

		float ReadFloat( string name )
		{
			reader.Position = 0;
			tree.SeekField( reader, name );
			return reader.ReadSingle();
		}

		bool ReadBool( string name )
		{
			if ( !tree.HasField( name ) ) return false;
			reader.Position = 0;
			tree.SeekField( reader, name );
			return reader.ReadBoolean();
		}
	}

	static bool IsPrintController( UnityAssetPointer script )
	{
		using var reader = Open( script.File, script.Asset );
		var tree = script.Asset.Tree;
		tree.SeekField( reader, "m_ClassName" );
		if ( reader.ReadAlignedString() != "PrintController" ) return false;
		reader.Position = 0;
		tree.SeekField( reader, "m_AssemblyName" );
		return reader.ReadAlignedString() == "RoR2.dll";
	}

	static UnityReader Open( UnityAssetFile file, SerializedFile.Object asset )
	{
		const int maxSize = 4 * 1024 * 1024;
		if ( asset.Size > maxSize ) throw new InvalidDataException( "Unity print animation metadata is too large." );
		return new( file.ReadObject( asset, maxSize ), leaveOpen: false );
	}
}

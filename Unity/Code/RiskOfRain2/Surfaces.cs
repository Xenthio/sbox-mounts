namespace UnityMount;

static class RiskOfRain2Surfaces
{
	internal const string MaterialTag = "ror2_material_";

	public static UnitySurfaceDefinition? Read( BundleIndex bundles, UnityAssetPointer source )
	{
		var (file, asset) = source;
		if ( asset.Tree is not { } tree || !tree.HasField( "m_Name" ) || !tree.HasField( "materialSwitchString" ) )
			return null;
		try
		{
			using var reader = new UnityReader( file.ReadObject( asset, 4096 ), leaveOpen: false );
			tree.SeekField( reader, "m_Name" );
			var name = reader.ReadAlignedString();
			reader.Position = 0;
			tree.SeekField( reader, "materialSwitchString" );
			var material = reader.ReadAlignedString();
			var audio = material switch
			{
				"dirt" => AudioSurface.Soil,
				"grass" => AudioSurface.Curtain,
				"metal" or "solus" => AudioSurface.Metal,
				"snow" => AudioSurface.Snow,
				"stone" => AudioSurface.Rock,
				"water_shallow" => AudioSurface.Water,
				"wood" => AudioSurface.Wood,
				_ => AudioSurface.Generic
			};
			reader.Position = 0;
			tree.SeekField( reader, "approximateColor" );
			var tint = UnityEffectValues.ReadColor( reader );
			reader.Position = 0;
			tree.SeekField( reader, "footstepEffectPrefab" );
			var effect = bundles.TryResolve( file, UnityReference.Read( reader ), UnitySceneObjects.GameObjectClassId, out var owner, out var prefab )
				? new UnityAssetPointer( owner, prefab ) : (UnityAssetPointer?)null;
			var boost = 0f;
			if ( tree.HasField( "speedBoost" ) )
			{
				reader.Position = 0;
				tree.SeekField( reader, "speedBoost" );
				boost = reader.ReadSingle();
			}
			return new( asset.Id, name, MaterialTag + material, audio, tint, effect, float.IsFinite( boost ) ? boost : 0f );
		}
		catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
		{
			Log.Warning( $"Could not read Risk of Rain 2 surface {asset.Id} in '{file.Entry.Name}': {e.Message}" );
			return null;
		}
	}
}

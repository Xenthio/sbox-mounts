namespace UnityMount;

static class RiskOfRain2EffectMetadata
{
	public static void Read( BundleIndex bundles, UnityAssetFile[] files, UnityVisualOverrides visuals )
	{
		foreach ( var file in files )
		foreach ( var asset in file.Data.Objects )
		{
			if ( asset.ClassId != SerializedFile.MonoBehaviourClassId || asset.Tree is not { } tree ||
				!tree.HasField( "effectIndex" ) || !tree.HasField( "positionAtReferencedTransform" ) ||
				!tree.HasField( "parentToReferencedTransform" ) ) continue;

			try
			{
				using var reader = new UnityReader( file.ReadObject( asset, 64 * 1024 ), leaveOpen: false );
				if ( !UnitySceneObjects.ReadComponentEnabled( reader, tree ) ) continue;
				reader.Position = 0;
				tree.SeekField( reader, "positionAtReferencedTransform" );
				var align = reader.ReadBoolean();
				reader.Position = 0;
				tree.SeekField( reader, "parentToReferencedTransform" );
				var follow = reader.ReadBoolean();
				if ( !align && !follow ) continue;
				reader.Position = 0;
				tree.SeekField( reader, "m_GameObject" );
				if ( bundles.TryResolve( file, UnityReference.Read( reader ), UnitySceneObjects.GameObjectClassId, out var ownerFile, out var owner ) )
					visuals.EffectAttachments.TryAdd( new( ownerFile, owner ), new( align, follow ) );
			}
			catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
			{
				Log.Warning( $"Could not read Risk of Rain 2 effect metadata {asset.Id} in '{file.Entry.Name}': {e.Message}" );
			}
		}
	}
}

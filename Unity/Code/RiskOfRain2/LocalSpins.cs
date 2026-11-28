namespace UnityMount;

static class RiskOfRain2LocalSpins
{
	public static void Read( BundleIndex bundles, UnityAssetFile[] files, UnityVisualOverrides visuals )
	{
		var scripts = new Dictionary<UnityAssetPointer, bool>();
		foreach ( var file in files )
		foreach ( var asset in file.Data.Objects )
		{
			if ( asset.ClassId != SerializedFile.MonoBehaviourClassId || asset.Tree is not { } tree ||
				!tree.HasField( "rotationSpeed" ) || !tree.HasField( "randomRotation" ) || !tree.HasField( "rotationSpeedMin" ) ) continue;

			try
			{
				using var reader = Open( file, asset );
				if ( !UnitySceneObjects.ReadComponentEnabled( reader, tree ) ) continue;
				reader.Position = 0;
				tree.SeekField( reader, "m_Script" );
				if ( !bundles.TryResolve( file, UnityReference.Read( reader ), SerializedFile.MonoScriptClassId,
					out var scriptFile, out var script ) || script.Tree is null ) continue;
				var key = new UnityAssetPointer( scriptFile, script );
				if ( !scripts.TryGetValue( key, out var matches ) )
					scripts[key] = matches = IsRotateObject( key );
				if ( !matches ) continue;

				reader.Position = 0;
				tree.SeekField( reader, "m_GameObject" );
				if ( !bundles.TryResolve( file, UnityReference.Read( reader ), UnitySceneObjects.GameObjectClassId,
					out var ownerFile, out var owner ) ) continue;

				reader.Position = 0;
				tree.SeekField( reader, "randomRotation" );
				var minimum = reader.ReadBoolean() ? ReadSpeed( "rotationSpeedMin" ) : (Vector3?)null;
				visuals.LocalSpins.TryAdd( new( ownerFile, owner ), new( ReadSpeed( "rotationSpeed" ), minimum ) );

				Vector3 ReadSpeed( string field )
				{
					reader.Position = 0;
					tree.SeekField( reader, field );
					var speed = new Vector3( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
					if ( !float.IsFinite( speed.LengthSquared ) ) throw new InvalidDataException( "Invalid Unity rotation speed." );
					return speed;
				}
			}
			catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
			{
				Log.Warning( $"Could not read Risk of Rain 2 rotation {asset.Id} in '{file.Entry.Name}': {e.Message}" );
			}
		}
	}

	static bool IsRotateObject( UnityAssetPointer script )
	{
		using var reader = Open( script.File, script.Asset );
		var tree = script.Asset.Tree;
		tree.SeekField( reader, "m_ClassName" );
		if ( reader.ReadAlignedString() != "RotateObject" ) return false;
		reader.Position = 0;
		tree.SeekField( reader, "m_AssemblyName" );
		return reader.ReadAlignedString() == "RoR2.dll";
	}

	static UnityReader Open( UnityAssetFile file, SerializedFile.Object asset ) =>
		new( file.ReadObject( asset, 64 * 1024 ), leaveOpen: false );
}

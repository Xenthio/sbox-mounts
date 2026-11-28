namespace UnityMount;

static class RiskOfRain2HurtboxMetadata
{
	public const string Main = "ror2_hurtbox_main";
	public const string Bullseye = "ror2_hurtbox_bullseye";
	public const string SniperTarget = "ror2_hurtbox_sniper_target";
	public const string Weak = "ror2_hurtbox_weak";
	const int WeakDamageModifier = 2;
	static readonly string[] Tags = [Main, Bullseye, SniperTarget, Weak];

	public static void Read( BundleIndex bundles, UnityAssetFile[] files, UnityVisualOverrides visuals )
	{
		foreach ( var file in files )
		foreach ( var group in file.Data.Objects )
		{
			if ( group.ClassId != SerializedFile.MonoBehaviourClassId || group.Tree?.HasField( "hurtBoxes" ) != true ||
				!group.Tree.HasField( "mainHurtBox" ) ) continue;
			try
			{
				using var reader = ReadMetadata( file, group );
				group.Tree.SeekField( reader, "mainHurtBox" );
				var main = UnityReference.Read( reader );
				reader.Position = 0;
				group.Tree.SeekField( reader, "hurtBoxes" );
				foreach ( var reference in UnityReference.ReadArray( reader ) )
					ReadHurtbox( bundles, file, reference, reference == main, visuals );
			}
			catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
			{
				Log.Warning( $"Could not read Risk of Rain 2 hurtbox group {group.Id} in '{file.Entry.Name}': {e.Message}" );
			}
		}
	}

	public static void CopyTags( GameObject original, ManualHitbox hitbox )
	{
		foreach ( var tag in Tags )
			if ( original.Tags.Has( tag, includeAncestors: false ) ) hitbox.HitboxTags.Add( tag );
	}

	static void ReadHurtbox( BundleIndex bundles, UnityAssetFile groupFile, UnityReference reference, bool main, UnityVisualOverrides visuals )
	{
		if ( !bundles.TryResolve( groupFile, reference, SerializedFile.MonoBehaviourClassId, out var file, out var asset ) ||
			asset.Tree is not { } tree || !tree.HasField( "damageModifier" ) ) return;
		using var reader = ReadMetadata( file, asset );
		tree.SeekField( reader, "m_GameObject" );
		if ( !bundles.TryResolve( file, UnityReference.Read( reader ), UnitySceneObjects.GameObjectClassId, out var owner, out var target ) )
			return;

		var tags = new List<string>();
		if ( main ) tags.Add( Main );
		if ( ReadFlag( "isBullseye" ) ) tags.Add( Bullseye );
		if ( ReadFlag( "isSniperTarget" ) ) tags.Add( SniperTarget );
		reader.Position = 0;
		tree.SeekField( reader, "damageModifier" );
		if ( reader.ReadInt32() == WeakDamageModifier ) tags.Add( Weak );
		if ( tags.Count > 0 ) visuals.ObjectTags.TryAdd( new( owner, target ), tags.ToArray() );

		bool ReadFlag( string name )
		{
			if ( !tree.HasField( name ) ) return false;
			reader.Position = 0;
			tree.SeekField( reader, name );
			return reader.ReadBoolean();
		}
	}

	static UnityReader ReadMetadata( UnityAssetFile file, SerializedFile.Object asset ) =>
		new( file.ReadObject( asset, 64 * 1024 ), leaveOpen: false );
}

namespace UnityMount;

sealed partial class UnityResourceRegistry
{
	public void RegisterSpeedTreeAssets( UnityAssetFile file )
	{
		var roots = new List<(string Name, UnityAssetPointer Root, UnityAssetPointer Transform)>();
		var seen = new HashSet<UnityAssetPointer>();
		var owners = new Dictionary<UnityAssetFile, HashSet<long>>();
		foreach ( var entry in ReadContainerAssets( file ) )
		{
			if ( !Path.GetExtension( entry.Path ).Equals( ".spm", StringComparison.OrdinalIgnoreCase ) ||
				!bundles.TryResolve( file, entry.Reference, UnitySceneObjects.GameObjectClassId, out var owner, out var asset ) ||
				asset.Tree is null || !seen.Add( new( owner, asset ) ) ) continue;
			var name = SanitizeResourceName( Path.GetFileNameWithoutExtension( entry.Path ) );
			if ( !IsResourceName( name ) ) continue;

			var root = new UnityAssetPointer( owner, asset );
			try
			{
				if ( FindRootTransform( root ) is not { } transform ) continue;
				CollectSpeedTreeOwners( transform, owners, new(), 0 );
				roots.Add( (name, root, transform) );
			}
			catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
			{
				Log.Warning( $"Could not index Unity SpeedTree '{entry.Path}': {e.Message}" );
			}
		}

		foreach ( var (owner, ids) in owners ) RegisterModels( owner, staticOwners: ids );
		foreach ( var (name, root, transform) in roots )
		{
			try
			{
				var node = ReadPrefab( transform.File, transform.Asset, new(), 0 );
				var preview = RegisterSpeedTreeModel( file.Group, name, root.Asset.Id, node );
				var path = RegisterPrefab( $"prefabs/{file.Group}/{name}", root.Asset.Id, node, preview: preview );
				if ( path is not null ) _prefabPaths.TryAdd( root, path );
			}
			catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
			{
				Log.Warning( $"Could not register Unity SpeedTree '{name}' in '{file.Entry.Name}': {e.Message}" );
			}
		}
	}

	void CollectSpeedTreeOwners( UnityAssetPointer transform, Dictionary<UnityAssetFile, HashSet<long>> owners,
		HashSet<UnityAssetPointer> visited, int depth )
	{
		if ( depth > 128 || visited.Count >= 16384 || !visited.Add( transform ) )
			throw new InvalidDataException( "Invalid Unity SpeedTree hierarchy." );
		var data = ReadTransform( transform.File, transform.Asset );
		if ( bundles.TryResolve( transform.File, data.Owner, UnitySceneObjects.GameObjectClassId, out var file, out var owner ) )
		{
			if ( !owners.TryGetValue( file, out var ids ) ) owners.Add( file, ids = new() );
			ids.Add( owner.Id );
		}

		foreach ( var reference in data.Children )
			if ( bundles.TryResolve( transform.File, reference, UnitySceneObjects.TransformClassId, out var childFile, out var child ) &&
				child.Tree is not null )
				CollectSpeedTreeOwners( new( childFile, child ), owners, visited, depth + 1 );
	}
}

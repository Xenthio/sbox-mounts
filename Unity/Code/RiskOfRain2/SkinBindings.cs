namespace UnityMount;

/// <summary>Binds a shared skin to the corresponding objects on another copy of its model.</summary>
static class RiskOfRain2SkinBindings
{
	public static Dictionary<UnityAssetPointer, UnityAssetPointer> Match( BundleIndex bundles, UnityAssetPointer source, UnityAssetPointer target )
	{
		var originals = Index( bundles, source );
		var copies = Index( bundles, target );
		var bindings = new Dictionary<UnityAssetPointer, UnityAssetPointer>();
		foreach ( var (key, original) in originals )
			if ( original is { } from && copies.GetValueOrDefault( key ) is { } to )
				bindings.TryAdd( from, to );
		return bindings;
	}

	static Dictionary<(string Path, int ClassId), UnityAssetPointer?> Index( BundleIndex bundles, UnityAssetPointer root )
	{
		var objects = new Dictionary<(string Path, int ClassId), UnityAssetPointer?>();
		var visited = new HashSet<UnityAssetPointer>();
		Visit( root, "", 0 );
		return objects;

		void Visit( UnityAssetPointer pointer, string parent, int depth )
		{
			if ( pointer.Asset.Tree is not { } tree ) return;
			if ( depth > 128 || visited.Count >= 16384 || !visited.Add( pointer ) )
				throw new InvalidDataException( "Invalid Unity skin model hierarchy." );
			using var reader = new UnityReader( pointer.File.ReadObject( pointer.Asset, 4096 ), leaveOpen: false );
			var gameObject = UnitySceneObjects.ReadGameObject( reader, tree );
			if ( gameObject.Name.Contains( '/' ) ) return;
			var path = parent + "/" + gameObject.Name;
			Add( pointer );
			foreach ( var component in gameObject.Components )
			{
				if ( bundles.TryResolve( pointer.File, component, UnitySkin.RendererClassId, out var file, out var asset ) ||
					bundles.TryResolve( pointer.File, component, UnityMesh.RendererClassId, out file, out asset ) ||
					bundles.TryResolve( pointer.File, component, UnityParticleEmitter.RendererClassId, out file, out asset ) )
					Add( new( file, asset ) );
				else if ( bundles.TryResolve( pointer.File, component, UnitySceneObjects.TransformClassId, out file, out asset ) && asset.Tree is { } layout )
				{
					using var transform = new UnityReader( file.ReadObject( asset, 16 * 1024 ), leaveOpen: false );
					layout.SeekField( transform, "m_Children" );
					foreach ( var child in UnityReference.ReadArray( transform ) )
					{
						if ( !bundles.TryResolve( file, child, UnitySceneObjects.TransformClassId, out var childFile, out var childAsset ) ||
							childAsset.Tree is not { } childLayout ) continue;
						using var childTransform = new UnityReader( childFile.ReadObject( childAsset, 4096 ), leaveOpen: false );
						childLayout.SeekField( childTransform, "m_GameObject" );
						if ( bundles.TryResolve( childFile, UnityReference.Read( childTransform ), UnitySceneObjects.GameObjectClassId, out var ownerFile, out var owner ) )
							Visit( new( ownerFile, owner ), path, depth + 1 );
					}
				}
			}

			void Add( UnityAssetPointer asset )
			{
				var key = (path, asset.Asset.ClassId);
				if ( !objects.TryAdd( key, asset ) ) objects[key] = null;
			}
		}
	}
}

namespace UnityMount;

sealed partial class UnityResourceRegistry
{
	readonly Dictionary<UnityAssetPointer, UnityPrefabCollider?> _colliders = new();
	readonly Dictionary<(UnityAssetPointer Mesh, bool Convex), string> _collisionModels = new();
	readonly Dictionary<UnitySurfaceDefinition, string> _surfacePaths = new();

	string RegisterSurface( UnitySurfaceDefinition surface )
	{
		if ( _surfacePaths.TryGetValue( surface, out var cached ) ) return cached;
		if ( !IsResourceName( surface.Name ) || ReservePath( $"surfaces/{surface.Name}", ".surface", surface.Id ) is not { } path )
			return _surfacePaths[surface] = null;
		var effect = surface.FootstepEffect is { } prefab ? FindPrefabPath( prefab ) : null;
		Define( ResourceType.GameResource, path, () => new UnitySurfaceLoader( surface, effect ) );
		return _surfacePaths[surface] = ResourcePath( path );
	}

	UnityAssetPointer? ResolveCollider( UnityAssetFile file, UnityReference reference )
	{
		if ( (bundles.TryResolve( file, reference, UnityCollider.BoxClassId, out var owner, out var asset ) ||
			bundles.TryResolve( file, reference, UnityCollider.SphereClassId, out owner, out asset ) ||
			bundles.TryResolve( file, reference, UnityCollider.CapsuleClassId, out owner, out asset ) ||
			bundles.TryResolve( file, reference, UnityCollider.MeshClassId, out owner, out asset )) && asset.Tree is not null )
			return new( owner, asset );
		return null;
	}

	UnityPrefabCollider? ReadCollider( UnityAssetPointer source )
	{
		if ( _colliders.TryGetValue( source, out var cached ) ) return cached;
		try
		{
			using var reader = ReadMetadata( source.File, source.Asset );
			var shape = UnityCollider.Read( reader, source.Asset.Tree, source.Asset.ClassId );
			if ( shape is UnityCollider.Mesh mesh )
			{
				if ( !bundles.TryResolve( source.File, mesh.Reference, UnityMesh.ClassId, out var file, out var asset ) || asset.Tree is null )
					return _colliders[source] = null;
				var pointer = new UnityAssetPointer( file, asset );
				var model = RegisterCollisionModel( pointer, mesh.Convex );
				return _colliders[source] = model is null ? null : new( shape, model, Mesh: pointer );
			}
			return _colliders[source] = shape is null ? null : new( shape );
		}
		catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
		{
			Log.Warning( $"Could not index Unity collider {source.Asset.Id} in '{source.File.Entry.Name}': {e.Message}" );
			return _colliders[source] = null;
		}
	}

	string RegisterCollisionModel( UnityAssetPointer pointer, bool convex )
	{
		var (file, asset) = pointer;
		var key = (pointer, convex);
		if ( _collisionModels.TryGetValue( key, out var cached ) ) return cached;

		using var reader = new UnityReader( file.ReadObject( asset, 4104 ), leaveOpen: false );
		asset.Tree.SeekField( reader, "m_Name" );
		var name = reader.ReadAlignedString();
		if ( !IsResourceName( name ) ) return _collisionModels[key] = null;

		var kind = convex ? "convex" : "mesh";
		var path = ReservePath( $"collision/{file.Group}/{name}_{kind}", ".vmdl", asset.Id );
		if ( path is null ) return _collisionModels[key] = null;
		Define( ResourceType.Model, path, () => new UnityCollisionModelLoader( pointer, convex ) );
		return _collisionModels[key] = ResourcePath( path );
	}
}

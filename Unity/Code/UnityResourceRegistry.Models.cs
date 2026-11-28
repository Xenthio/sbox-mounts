namespace UnityMount;

sealed partial class UnityResourceRegistry
{
	readonly record struct RendererSource( UnityReference Mesh, UnityReference[] Materials, UnityReference[] Joints, long VariantId, bool Enabled, UnitySkin.VisibilityBounds? Bounds = null );
	readonly record struct MeshRendererSource( SerializedFile.Object Asset, bool Enabled, UnityReference[] Materials,
		UnitySurfaceRange? Surfaces, Transform? WorldTransform );
	readonly Dictionary<UnityAssetPointer, RendererSource> _rendererSources = new();
	readonly Dictionary<(UnityVisualOverrides.Skin Skin, UnityAssetPointer Renderer), UnityPrefabRenderer?> _variantRenderers = new();

	UnityPrefabRenderer? RegisterVariantRenderer( UnityAssetPointer renderer, UnityVisualOverrides.Skin skin )
	{
		var key = (skin, renderer);
		if ( _variantRenderers.TryGetValue( key, out var cached ) ) return cached;
		return _variantRenderers[key] = BuildVariantRenderer( renderer, skin );
	}

	UnityPrefabRenderer? BuildVariantRenderer( UnityAssetPointer renderer, UnityVisualOverrides.Skin skin )
	{
		var visual = skin.Visuals.Renderers.GetValueOrDefault( renderer );
		if ( Equals( visual, overrides.Renderers.GetValueOrDefault( renderer ) ) )
			return _prefabRenderers.TryGetValue( (renderer.File, renderer.Asset.Id), out var original ) ? original : null;

		if ( !_rendererSources.TryGetValue( renderer, out var source ) || ResolveMesh( renderer.File, source.Mesh, visual ) is not { } mesh ) return null;

		var materials = ResolveMaterials( renderer.File, source.Materials, visual?.Material );
		var directory = $"models/{renderer.File.Group}/skins/{skin.Name}";
		string path;
		UnityPrefabJoint[] prefabJoints = null;
		if ( source.Joints is null )
		{
			path = RegisterModel( directory, mesh.File, mesh.Asset, materials, source.VariantId );
		}
		else
		{
			var joints = ResolveJoints( renderer, source.Joints, mesh, out prefabJoints );
			if ( joints is null ) return null;

			var controller = ResolveController( renderer, prefabJoints ) is { } animationController
				? GetControllerAnimations( animationController ) : null;
			var animations = controller?.Clips ?? CollectAnimations( renderer.File );
			path = RegisterModel( $"{directory}/skinned", mesh.File, mesh.Asset, materials, source.VariantId,
				new( joints, source.Bounds ), animations, controller: controller );
		}
		return path is null ? null : new( path, source.Joints is not null, source.Enabled, Joints: prefabJoints );
	}

	void RegisterModels( UnityAssetFile file, UnityStaticBatchCache staticBatches = null,
		IReadOnlyDictionary<UnityAssetPointer, UnityAnimationSource[]> timelineClips = null, HashSet<long> staticOwners = null )
	{
		var renderers = new Dictionary<long, MeshRendererSource>();
		foreach ( var asset in file.Data.Objects.Where( item => item.ClassId == UnityMesh.RendererClassId && item.Tree is not null ) )
		{
			try
			{
				using var reader = ReadMetadata( file, asset );
				asset.Tree.SeekField( reader, "m_GameObject" );
				var owner = UnityReference.Read( reader );
				if ( !owner.IsLocal || staticOwners?.Contains( owner.Id ) == false ) continue;
				var enabled = UnitySceneObjects.ReadComponentEnabled( reader, asset.Tree );
				var materials = ReadReferences( reader, asset.Tree, "m_Materials" );
				var (surfaces, world) = staticBatches is null ? default : ReadStaticBatch( file, asset, reader );
				renderers.TryAdd( owner.Id, new( asset, enabled, materials, surfaces, world ) );
			}
			catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
			{
				Log.Warning( $"Could not index Unity renderer {asset.Id} in '{file.Entry.Name}': {e.Message}" );
			}
		}

		var variants = new Dictionary<(UnityAssetFile File, long Mesh, string Materials, UnitySurfaceRange? Surfaces), string>();
		foreach ( var filter in file.Data.Objects.Where( item => item.ClassId == UnityMesh.FilterClassId && item.Size == 24 ) )
		{
			try
			{
				using var reader = ReadMetadata( file, filter );
				var owner = UnityReference.Read( reader );
				var reference = UnityReference.Read( reader );
				if ( !owner.IsLocal || !renderers.TryGetValue( owner.Id, out var renderer ) ) continue;

				var key = new UnityAssetPointer( file, renderer.Asset );
				_rendererSources.TryAdd( key, new( reference, renderer.Materials, null, filter.Id, renderer.Enabled ) );
				var visual = overrides.Renderers.GetValueOrDefault( key );
				if ( ResolveMesh( file, reference, visual ) is not { } mesh ) continue;
				var materials = ResolveMaterials( file, renderer.Materials, visual?.Material );
				var variant = (mesh.File, mesh.Asset.Id, string.Join( '|', materials.Select( material => material.Path ) ), renderer.Surfaces);
				if ( !variants.TryGetValue( variant, out var path ) )
					variants[variant] = path = RegisterModel( $"models/{file.Group}", mesh.File, mesh.Asset, materials, filter.Id, surfaces: renderer.Surfaces, staticBatches: staticBatches );
				if ( path is not null )
					_prefabRenderers.TryAdd( (file, renderer.Asset.Id), new( path, false, renderer.Enabled, renderer.WorldTransform ) );
			}
			catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
			{
				Log.Warning( $"Could not index Unity mesh filter {filter.Id} in '{file.Entry.Name}': {e.Message}" );
			}
		}

		if ( staticOwners is null ) RegisterSkinnedModels( file, timelineClips );
	}

	(UnitySurfaceRange? Surfaces, Transform? World) ReadStaticBatch( UnityAssetFile file, SerializedFile.Object renderer, UnityReader reader )
	{
		if ( !renderer.Tree.HasField( "m_StaticBatchInfo" ) ) return default;
		reader.Position = 0;
		renderer.Tree.SeekField( reader, "m_StaticBatchInfo" );
		var first = reader.ReadUInt16();
		var count = reader.ReadUInt16();
		if ( count == 0 ) return default;

		reader.Position = 0;
		renderer.Tree.SeekField( reader, "m_StaticBatchRoot" );
		var reference = UnityReference.Read( reader );
		if ( reference.Id == 0 ) return (new( first, count ), Transform.Zero);
		if ( !bundles.TryResolve( file, reference, UnitySceneObjects.TransformClassId, out var rootFile, out var root ) || root.Tree is null )
			throw new InvalidDataException( "Unity static batch root could not be resolved." );
		return (new( first, count ), ReadWorldTransform( rootFile, root, 0 ));
	}

	Transform ReadWorldTransform( UnityAssetFile file, SerializedFile.Object asset, int depth )
	{
		if ( depth > 128 ) throw new InvalidDataException( "Invalid Unity static batch hierarchy." );
		var node = ReadTransform( file, asset );
		if ( node.Parent.Id == 0 ) return node.Local;
		if ( !bundles.TryResolve( file, node.Parent, UnitySceneObjects.TransformClassId, out var parentFile, out var parent ) || parent.Tree is null )
			throw new InvalidDataException( "Unity static batch parent could not be resolved." );
		return ReadWorldTransform( parentFile, parent, depth + 1 ).ToWorld( node.Local );
	}

	string RegisterModel( string directory, UnityAssetFile file, SerializedFile.Object asset,
		(string Name, string Path)[] materials, long rendererId, UnitySkin skin = null, UnityAnimationSource[] animations = null,
		UnitySurfaceRange? surfaces = null, UnityStaticBatchCache staticBatches = null, UnityControllerAnimations controller = null )
	{
		using var data = file.ReadObject( asset, 16 * 1024 );
		using var reader = new UnityReader( data );
		asset.Tree.SeekField( reader, "m_Name" );
		var name = reader.ReadAlignedString();
		if ( surfaces is { } batch ) name = $"static_batch_{asset.Id:x}_{batch.First}_{batch.Count}";
		else if ( !IsResourceName( name ) ) name = SanitizeResourceName( name );
		if ( !IsResourceName( name ) || !UnityMesh.HasSurfaces( reader, asset.Tree ) ) return null;

		var label = materials.FirstOrDefault().Name ?? "default";
		var path = ReservePath( $"{directory}/{name}/{label}", ".vmdl", rendererId );
		if ( path is null ) return null;
		var materialPaths = materials.Select( material => material.Path ).ToArray();
		Define( ResourceType.Model, path, () => new UnityModelLoader( new( file, asset ), materialPaths,
			skin, animations, surfaces, skin is null ? null : FindEventPrefab, surfaces.HasValue ? staticBatches : null,
			controller, overrides.InPlaceAnimations ) );
		return ResourcePath( path );
	}

	UnityAssetPointer? ResolveMesh( UnityAssetFile file, UnityReference reference, UnityVisualOverrides.Renderer visual )
	{
		if ( visual?.Mesh is { } mesh ) return mesh;
		return bundles.TryResolve( file, reference, UnityMesh.ClassId, out var meshFile, out var asset ) && asset.Tree is not null
			? new( meshFile, asset ) : null;
	}

	(string Name, string Path)[] ResolveMaterials( UnityAssetFile file, UnityReference[] slots, UnityAssetPointer? skinMaterial )
	{
		var materials = new (string Name, string Path)[skinMaterial is null ? slots.Length : Math.Max( 1, slots.Length )];
		for ( var i = 0; i < slots.Length; i++ )
			if ( bundles.TryResolve( file, slots[i], UnityMaterial.ClassId, out var materialFile, out var material ) )
				materials[i] = RegisterMaterial( materialFile, material );
		if ( skinMaterial is { } replacement )
			materials[0] = RegisterMaterial( replacement.File, replacement.Asset );
		return materials;
	}

	static UnityReference[] ReadReferences( UnityReader reader, UnityTypeTree tree, string field )
	{
		reader.Position = 0;
		tree.SeekField( reader, field );
		return UnityReference.ReadArray( reader );
	}
}

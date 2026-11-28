namespace UnityMount;

sealed partial class UnityResourceRegistry
{
	readonly Dictionary<(UnityAssetFile File, long Id), UnityPrefabRenderer> _prefabRenderers = new();
	readonly Dictionary<(UnityAssetFile File, long Id), UnitySceneObjects.GameObjectData> _gameObjects = new();
	readonly Dictionary<(UnityAssetFile File, long Id), UnitySceneObjects.TransformData> _transforms = new();
	readonly Dictionary<UnityAssetPointer, UnityLight> _lights = new();
	readonly Dictionary<UnityAssetPointer, string> _prefabPaths = new();

	public void RegisterContainerAssets( UnityAssetFile file )
	{
		var seen = new HashSet<(UnityAssetFile File, long Id)>();
		foreach ( var entry in ReadContainerAssets( file ) )
		{
			var extension = Path.GetExtension( entry.Path );
			if ( extension.Equals( ".anim", StringComparison.OrdinalIgnoreCase ) )
			{
				if ( bundles.TryResolve( file, entry.Reference, UnityAnimationClip.ClassId, out var clipFile, out var clipAsset ) &&
					seen.Add( (clipFile, clipAsset.Id) ) )
					RegisterLightClip( clipFile, clipAsset );
				continue;
			}
			if ( !extension.Equals( ".prefab", StringComparison.OrdinalIgnoreCase ) &&
				!extension.Equals( ".fbx", StringComparison.OrdinalIgnoreCase ) &&
				!extension.Equals( ".spm", StringComparison.OrdinalIgnoreCase ) ) continue;
			if ( !bundles.TryResolve( file, entry.Reference, UnitySceneObjects.GameObjectClassId, out var rootFile, out var rootAsset ) ||
				rootAsset.Tree is null || !seen.Add( (rootFile, rootAsset.Id) ) ) continue;
			var name = SanitizeResourceName( Path.GetFileNameWithoutExtension( entry.Path ) );
			if ( !IsResourceName( name ) ) continue;

			try
			{
				if ( FindRootTransform( new( rootFile, rootAsset ) ) is not { } transform ) continue;

				var modelSkins = new HashSet<UnityVisualOverrides.Skin>();
				var node = ReadPrefab( transform.File, transform.Asset, new(), 0, modelSkins );
				var preview = extension.Equals( ".spm", StringComparison.OrdinalIgnoreCase )
					? RegisterSpeedTreeModel( file.Group, name, rootAsset.Id, node ) : null;
				var path = RegisterPrefab( $"prefabs/{file.Group}/{name}", rootAsset.Id, node, preview: preview );
				if ( path is not null ) _prefabPaths.TryAdd( new( rootFile, rootAsset ), path );
				foreach ( var skin in modelSkins )
				{
					var variant = ReadPrefab( transform.File, transform.Asset, new(), 0, skin: skin );
					RegisterPrefab( $"prefabs/{file.Group}/skins/{name}/{skin.Name}", rootAsset.Id, variant,
						$"{node.Name} ({skin.Name})" );
				}
			}
			catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
			{
				Log.Warning( $"Could not index Unity prefab '{entry.Path}': {e.Message}" );
			}
		}
	}

	static IEnumerable<UnitySceneObjects.Asset> ReadContainerAssets( UnityAssetFile file )
	{
		foreach ( var container in file.Data.Objects.Where( asset => asset.ClassId == UnitySceneObjects.AssetBundleClassId && asset.Tree is not null ) )
		{
			using var reader = ReadMetadata( file, container );
			foreach ( var entry in UnitySceneObjects.ReadAssets( reader, container.Tree ) )
				yield return entry;
		}
	}

	string RegisterPrefab( string stem, long id, UnityPrefabNode node, string title = null, string preview = null )
	{
		preview ??= FindPreview( node );
		if ( preview is null && !HasPrefabComponents( node ) ) return null;
		if ( ReservePath( stem, ".prefab", id ) is not { } path ) return null;
		Define( ResourceType.PrefabFile, path, () => new UnityPrefabLoader( node, preview, title ) );
		return ResourcePath( path );
	}

	UnityAnimationEventPrefab FindEventPrefab( UnityAssetFile file, UnityReference reference )
	{
		if ( !bundles.TryResolve( file, reference, UnitySceneObjects.GameObjectClassId, out var prefabFile, out var prefab ) ) return default;
		var key = new UnityAssetPointer( prefabFile, prefab );
		return new( FindPrefabPath( key ), overrides.EffectAttachments.GetValueOrDefault( key ) );
	}

	string FindPrefabPath( UnityAssetPointer key ) => _prefabPaths.GetValueOrDefault( key ) ?? shared?.Prefabs.GetValueOrDefault( key );

	UnityPrefabNode ReadPrefab( UnityAssetFile file, SerializedFile.Object asset,
		HashSet<(UnityAssetFile File, long Id)> visited, int depth,
		HashSet<UnityVisualOverrides.Skin> modelSkins = null,
		UnityVisualOverrides.Skin skin = null, bool insideSkin = false, bool includeAttachments = true, SceneLayout scene = null )
	{
		// Scene roots can contain whole terrain and foliage layers.
		if ( depth > 128 || (scene is null && visited.Count >= 16384) || !visited.Add( (file, asset.Id) ) )
			throw new InvalidDataException( "Invalid Unity prefab hierarchy." );

		var source = ReadTransform( file, asset );
		if ( !bundles.TryResolve( file, source.Owner, UnitySceneObjects.GameObjectClassId, out var ownerFile, out var owner ) ||
			owner.Tree is null ) throw new InvalidDataException( "Unity transform has no GameObject." );

		var ownerKey = new UnityAssetPointer( ownerFile, owner );
		if ( modelSkins is not null && overrides.Skins.TryGetValue( ownerKey, out var found ) )
		{
			foreach ( var variant in found )
				if ( IsResourceName( variant.Name ) ) modelSkins.Add( variant );
		}

		// Apply a skin only inside its own model hierarchy.
		insideSkin |= skin?.Model == ownerKey;
		var appliedSkin = insideSkin ? skin : null;
		var gameObject = ReadGameObject( ownerFile, owner );
		var renderers = new List<UnityPrefabRenderer>();
		var lights = new List<UnityLight>();
		var colliders = new List<UnityPrefabCollider>();
		UnitySceneProbe? reflectionProbe = null;
		UnityPrefabSprite? sprite = null;
		UnityPrefabLine line = null;
		UnityPrefabLod lod = null;
		foreach ( var component in gameObject.Components )
		{
			if ( bundles.TryResolve( ownerFile, component, UnityMesh.RendererClassId, out var rendererFile, out var renderer ) ||
				bundles.TryResolve( ownerFile, component, UnitySkin.RendererClassId, out rendererFile, out renderer ) )
			{
				var rendererKey = new UnityAssetPointer( rendererFile, renderer );
				if ( appliedSkin is not null )
				{
					if ( RegisterVariantRenderer( rendererKey, appliedSkin ) is { } variant )
						renderers.Add( variant with { Source = rendererKey } );
				}
				else if ( _prefabRenderers.TryGetValue( (rendererFile, renderer.Id), out var binding ) )
				{
					renderers.Add( binding with { Source = rendererKey } );
				}
			}
			else if ( bundles.TryResolve( ownerFile, component, UnityBillboard.RendererClassId, out var billboardFile, out var billboard ) )
			{
				if ( ReadBillboard( billboardFile, billboard ) is { } binding ) renderers.Add( binding );
			}
			else if ( bundles.TryResolve( ownerFile, component, UnitySceneObjects.LodGroupClassId, out var lodFile, out var group ) &&
				group.Tree is not null )
			{
				lod = ReadPrefabLod( lodFile, group );
			}
			else if ( bundles.TryResolve( ownerFile, component, UnityLight.ClassId, out var lightFile, out var light ) &&
				light.Tree is not null && ReadLight( new( lightFile, light ) ) is { } descriptor )
			{
				lights.Add( overrides.LightAnimations.TryGetValue( ownerKey, out var animation )
					? descriptor with { Animation = animation }
					: descriptor );
			}
			else if ( bundles.TryResolve( ownerFile, component, UnitySpriteRenderer.ClassId, out var spriteFile, out var spriteRenderer ) &&
				spriteRenderer.Tree is not null )
			{
				sprite = ReadSpriteRenderer( spriteFile, spriteRenderer );
			}
			else if ( bundles.TryResolve( ownerFile, component, UnityLine.ClassId, out var lineFile, out var lineRenderer ) &&
				lineRenderer.Tree is not null )
			{
				line = ReadLine( lineFile, lineRenderer );
			}
			else if ( scene is not null && bundles.TryResolve( ownerFile, component, UnityReflectionProbe.ClassId, out var probeFile, out var probe ) &&
				probe.Tree is not null )
			{
				reflectionProbe = RegisterReflectionProbe( probeFile, probe );
			}
			else if ( ResolveCollider( ownerFile, component ) is { } colliderAsset &&
				ReadCollider( colliderAsset ) is { } collider )
			{
				var surface = scene is not null && scene.Markers.Surfaces.TryGetValue( ownerKey, out var definition )
					? RegisterSurface( definition ) : null;
				colliders.Add( collider with { SurfacePath = surface } );
			}
		}

		var visuals = appliedSkin?.Visuals ?? overrides;
		var children = new List<UnityPrefabNode>( source.Children.Length );
		foreach ( var reference in source.Children )
		{
			if ( !bundles.TryResolve( file, reference, UnitySceneObjects.TransformClassId, out var childFile, out var child ) ||
				child.Tree is null )
			{
				if ( bundles.TryResolve( file, reference, UnitySceneObjects.RectTransformClassId, out _, out _ ) )
					continue;
				throw new InvalidDataException( "Unity prefab child transform could not be resolved." );
			}
			children.Add( ReadPrefab( childFile, child, visited, depth + 1, modelSkins, skin, insideSkin, includeAttachments, scene ) );
		}
		if ( includeAttachments && visuals.Attachments.TryGetValue( ownerKey, out var attachments ) )
		{
			foreach ( var attachment in attachments )
			{
				if ( FindRootTransform( attachment.Prefab ) is not { } root ) continue;
				// The same effect can be attached to more than one socket.
				var node = ReadPrefab( root.File, root.Asset, new(), depth + 1, includeAttachments: false );
				children.Add( node with { Local = attachment.Local } );
			}
		}
		var active = visuals.Activations.GetValueOrDefault( ownerKey, gameObject.Active ) &&
			scene?.Markers.InactiveAtStart.Contains( ownerKey ) != true;
		var particle = ReadParticle( ownerFile, gameObject.Components, appliedSkin, new( file, asset ) );
		if ( particle?.Planes is { Length: > 0 } planes ) scene?.ParticlePlanes.UnionWith( planes );
		UnityAnimationSource[] clips = null;
		if ( scene is null && ResolveOwnedController( ownerKey ) is { } controller )
			clips = GetControllerClips( controller );
		var animateTransforms = clips is { Length: > 0 } && !renderers.Any( renderer => renderer.Skinned ) && !children.Any( HasSkinnedRenderers );
		return new( gameObject.Name, active, source.Local, gameObject.Layer, renderers.ToArray(), lights.ToArray(), particle,
			colliders.ToArray(), children.ToArray(), scene?.Markers.SpawnPoints.Contains( ownerKey ) == true, reflectionProbe, sprite, new( file, asset ),
			overrides.PrintAnimations.TryGetValue( ownerKey, out var print ) ? print : null, clips, animateTransforms, line, lod,
			overrides.ObjectTags.GetValueOrDefault( ownerKey ), overrides.OmniEffects.GetValueOrDefault( ownerKey ),
			overrides.LocalSpins.TryGetValue( ownerKey, out var spin ) ? spin : null );
	}

	UnityPrefabLod ReadPrefabLod( UnityAssetFile file, SerializedFile.Object asset )
	{
		try
		{
			using var reader = ReadMetadata( file, asset );
			if ( UnitySceneObjects.ReadLodGroup( reader, asset.Tree ) is not { } group ) return null;
			var levels = group.Levels.Select( level => new UnityPrefabLod.Level( level.ScreenHeight,
				level.Renderers.Select( ResolveRenderer ).OfType<UnityAssetPointer>().Distinct().ToArray(),
				level.Renderers.Any( reference => reference.Id != 0 ) ) ).ToArray();
			return new( group.Center, group.Size, levels );
		}
		catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
		{
			Log.Warning( $"Could not read Unity LOD group {asset.Id} in '{file.Entry.Name}': {e.Message}" );
			return null;
		}

		UnityAssetPointer? ResolveRenderer( UnityReference reference ) =>
			bundles.TryResolve( file, reference, UnityMesh.RendererClassId, out var owner, out var renderer ) ||
			bundles.TryResolve( file, reference, UnitySkin.RendererClassId, out owner, out renderer ) ||
			bundles.TryResolve( file, reference, UnityBillboard.RendererClassId, out owner, out renderer ) ? new( owner, renderer ) : null;
	}

	static bool HasSkinnedRenderers( UnityPrefabNode node ) =>
		node.Renderers.Any( renderer => renderer.Skinned ) || node.Children.Any( HasSkinnedRenderers );

	UnityPrefabSprite? ReadSpriteRenderer( UnityAssetFile file, SerializedFile.Object asset )
	{
		try
		{
			using var reader = ReadMetadata( file, asset );
			if ( UnitySpriteRenderer.Read( reader, asset.Tree ) is not { } source ||
				!bundles.TryResolve( file, source.Sprite, UnitySprite.ClassId, out var owner, out var sprite ) ||
				RegisterSprite( new UnityAssetPointer( owner, sprite ) ) is not { } resource )
				return null;

			return new( resource, source );
		}
		catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
		{
			Log.Warning( $"Could not index Unity sprite renderer {asset.Id} in '{file.Entry.Name}': {e.Message}" );
			return null;
		}
	}

	static bool HasPrefabComponents( UnityPrefabNode node, bool parentEnabled = true )
	{
		var enabled = parentEnabled && node.Enabled;
		// Some indicators and collision volumes are enabled dynamically by the game.
		return node.Sprite is not null || node.Line is not null || node.Colliders.Length > 0 || (enabled && (node.Particle is not null || node.OmniEffect is not null ||
			node.Lights.Any( light => light.Enabled && light.Intensity > 0 ))) ||
			node.Children.Any( child => HasPrefabComponents( child, enabled ) );
	}

	UnityAssetPointer? FindRootTransform( UnityAssetPointer root )
	{
		if ( FindTransform( root ) is not { } transform ) return null;
		return ReadTransform( transform.File, transform.Asset ).Parent.Id == 0 ? transform : null;
	}

	UnityAssetPointer? FindTransform( UnityAssetPointer owner )
	{
		foreach ( var component in ReadGameObject( owner.File, owner.Asset ).Components )
		{
			if ( !bundles.TryResolve( owner.File, component, UnitySceneObjects.TransformClassId, out var file, out var transform ) ||
				transform.Tree is null ) continue;
			return new( file, transform );
		}
		return null;
	}

	UnityLight ReadLight( UnityAssetPointer source )
	{
		if ( _lights.TryGetValue( source, out var cached ) ) return cached;
		try
		{
			using var reader = ReadMetadata( source.File, source.Asset );
			return _lights[source] = UnityLight.Read( reader, source.Asset.Tree );
		}
		catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
		{
			Log.Warning( $"Could not index Unity light {source.Asset.Id} in '{source.File.Entry.Name}': {e.Message}" );
			return _lights[source] = null;
		}
	}

	static string FindPreview( UnityPrefabNode node )
	{
		foreach ( var renderer in node.Renderers )
			if ( renderer.Enabled ) return renderer.Path;

		foreach ( var child in node.Children )
			if ( child.Enabled && FindPreview( child ) is { } preview ) return preview;

		return null;
	}

	UnitySceneObjects.GameObjectData ReadGameObject( UnityAssetFile file, SerializedFile.Object asset )
	{
		var key = (file, asset.Id);
		if ( _gameObjects.TryGetValue( key, out var result ) ) return result;
		using var reader = ReadMetadata( file, asset );
		return _gameObjects[key] = UnitySceneObjects.ReadGameObject( reader, asset.Tree );
	}

	UnitySceneObjects.TransformData ReadTransform( UnityAssetFile file, SerializedFile.Object asset )
	{
		var key = (file, asset.Id);
		if ( _transforms.TryGetValue( key, out var result ) ) return result;
		using var reader = ReadMetadata( file, asset );
		return _transforms[key] = UnitySceneObjects.ReadTransform( reader, asset.Tree );
	}
}

namespace UnityMount;

sealed partial class UnityResourceRegistry
{
	readonly record struct JointTransform( string Name, UnityReference Parent, Transform Local );
	readonly Dictionary<(UnityAssetFile File, long Id), JointTransform> _jointTransforms = new();
	readonly Dictionary<UnityAssetPointer, UnityAssetPointer?> _controllers = new();
	readonly Dictionary<UnityAssetPointer, UnityControllerAnimations> _controllerAnimations = new();
	readonly Dictionary<UnityAssetPointer, UnityAvatar> _avatars = new();
	readonly Dictionary<UnityAssetPointer, Dictionary<uint, uint>> _avatarPathAliases = new();

	void RegisterSkinnedModels( UnityAssetFile file, IReadOnlyDictionary<UnityAssetPointer, UnityAnimationSource[]> timelineClips )
	{
		var variants = new Dictionary<(UnityAssetFile File, long Mesh, string Materials, string Joints, UnityAssetPointer? Controller,
			UnitySkin.VisibilityBounds? Bounds, UnityAssetPointer? TimelineRig), string>();
		UnityAnimationSource[] animations = null;
		foreach ( var renderer in file.Data.Objects.Where( item => item.ClassId == UnitySkin.RendererClassId && item.Tree is not null ) )
		{
			try
			{
				using var reader = ReadMetadata( file, renderer );
				renderer.Tree.SeekField( reader, "m_Mesh" );
				var reference = UnityReference.Read( reader );
				var key = new UnityAssetPointer( file, renderer );
				var poses = ReadReferences( reader, renderer.Tree, "m_Bones" );
				var slots = ReadReferences( reader, renderer.Tree, "m_Materials" );
				var enabled = UnitySceneObjects.ReadComponentEnabled( reader, renderer.Tree );
				var bounds = ReadSkinnedBounds( key, reader, poses );
				_rendererSources.TryAdd( key, new( reference, slots, poses, renderer.Id, enabled, bounds ) );
				var visual = overrides.Renderers.GetValueOrDefault( key );
				if ( ResolveMesh( file, reference, visual ) is not { } mesh ) continue;

				var joints = ResolveJoints( key, poses, mesh, out var prefabJoints );
				if ( joints is null ) continue;

				var controller = ResolveController( key, prefabJoints );
				var timelineRig = prefabJoints is { Length: > 0 } && timelineClips is { Count: > 0 }
					? FindTimelineRig( prefabJoints[0].Transform, timelineClips ) : null;
				var materials = ResolveMaterials( file, slots, visual?.Material );
				var signature = string.Join( '|', joints.Select( joint => $"{joint.Name}:{joint.Parent}:{joint.PathHashes[0]}" ) );
				var variant = (mesh.File, mesh.Asset.Id, string.Join( '|', materials.Select( item => item.Path ) ), signature, controller, bounds, timelineRig);
				if ( !variants.TryGetValue( variant, out var path ) )
				{
					var controllerAnimations = controller is { } source ? GetControllerAnimations( source ) : null;
					var clips = controllerAnimations?.Clips ?? (animations ??= CollectAnimations( file ));
					if ( timelineRig is { } rig ) clips = clips.Concat( timelineClips[rig] ).Distinct().ToArray();
					variants[variant] = path = RegisterModel( $"models/{file.Group}/skinned", mesh.File, mesh.Asset, materials, renderer.Id,
						new( joints, bounds ), clips, controller: controllerAnimations );
				}
				if ( path is not null )
					_prefabRenderers.Add( (file, renderer.Id), new( path, true, enabled, Joints: prefabJoints, TimelineRig: timelineRig ) );
			}
			catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
			{
				Log.Warning( $"Could not index Unity skinned renderer {renderer.Id} in '{file.Entry.Name}': {e.Message}" );
			}
		}
	}

	UnityAssetPointer? FindTimelineRig( UnityAssetPointer transform,
		IReadOnlyDictionary<UnityAssetPointer, UnityAnimationSource[]> timelineClips )
	{
		for ( var depth = 0; depth < 128; depth++ )
		{
			if ( timelineClips.ContainsKey( transform ) ) return transform;
			var source = ReadTransform( transform.File, transform.Asset );
			if ( !bundles.TryResolve( transform.File, source.Parent, UnitySceneObjects.TransformClassId, out var file, out var parent ) ||
				parent.Tree is null ) return null;
			transform = new( file, parent );
		}
		return null;
	}

	UnitySkin.VisibilityBounds? ReadSkinnedBounds( UnityAssetPointer renderer, UnityReader reader, UnityReference[] poses )
	{
		var tree = renderer.Asset.Tree;
		if ( UnitySkin.ReadLocalBounds( reader, tree ) is not { } bounds ) return null;
		if ( !tree.HasField( "m_RootBone" ) ) return new( bounds, Transform.Zero, -1 );

		reader.Position = 0;
		tree.SeekField( reader, "m_RootBone" );
		var root = UnityReference.Read( reader );
		if ( root.Id == 0 ) return new( bounds, Transform.Zero, -1 );

		// An explicit root makes the serialized box bone-local. The native model expects mesh space.
		if ( !bundles.TryResolve( renderer.File, root, UnitySceneObjects.TransformClassId, out var rootFile, out var rootAsset ) ||
			rootAsset.Tree is null || ReadWorldTransform( new( rootFile, rootAsset ) ) is not { } rootWorld ||
			FindRendererTransform( renderer ) is not { } mesh || ReadWorldTransform( mesh ) is not { } meshWorld ) return null;

		var spaceToMesh = meshWorld.ToLocal( rootWorld );
		var inMesh = bounds.Transform( spaceToMesh );
		if ( !float.IsFinite( inMesh.Mins.LengthSquared ) || !float.IsFinite( inMesh.Maxs.LengthSquared ) ) return null;
		return new( bounds, spaceToMesh, Array.IndexOf( poses, root ) );
	}

	UnityAssetPointer? FindRendererTransform( UnityAssetPointer renderer )
	{
		using var reader = ReadMetadata( renderer.File, renderer.Asset );
		renderer.Asset.Tree.SeekField( reader, "m_GameObject" );
		var reference = UnityReference.Read( reader );
		return bundles.TryResolve( renderer.File, reference, UnitySceneObjects.GameObjectClassId, out var ownerFile, out var owner ) &&
			owner.Tree is not null ? FindTransform( new( ownerFile, owner ) ) : null;
	}

	UnityAssetPointer? ResolveController( UnityAssetPointer renderer, UnityPrefabJoint[] joints )
	{
		// The mesh object can have its own material animator. Use the armature for bone clips.
		if ( joints is { Length: > 0 } && FindController( joints[0].Transform, 0 ) is { } rig )
			return rig;
		return FindRendererTransform( renderer ) is { } transform ? FindController( transform, 0 ) : null;
	}

	UnityAssetPointer? FindController( UnityAssetPointer transform, int depth )
	{
		if ( depth > 128 ) return null;
		if ( _controllers.TryGetValue( transform, out var cached ) ) return cached;

		var source = ReadTransform( transform.File, transform.Asset );
		if ( bundles.TryResolve( transform.File, source.Owner, UnitySceneObjects.GameObjectClassId, out var ownerFile, out var owner ) &&
			ResolveOwnedController( new( ownerFile, owner ) ) is { } controller )
			return _controllers[transform] = controller;

		return _controllers[transform] = bundles.TryResolve( transform.File, source.Parent, UnitySceneObjects.TransformClassId,
			out var parentFile, out var parent ) && parent.Tree is not null ? FindController( new( parentFile, parent ), depth + 1 ) : null;
	}

	UnityAssetPointer? ResolveOwnedController( UnityAssetPointer owner ) =>
		overrides.Controllers.TryGetValue( owner, out var controller ) ? controller :
		owner.Asset.Tree is not null ? ReadGameObjectController( owner.File, owner.Asset ) : null;

	UnityAssetPointer? ReadGameObjectController( UnityAssetFile file, SerializedFile.Object owner )
	{
		foreach ( var component in ReadGameObject( file, owner ).Components )
		{
			if ( !bundles.TryResolve( file, component, UnityAvatar.AnimatorClassId, out var animatorFile, out var animator ) ||
				animator.Tree?.HasField( "m_Controller" ) != true ) continue;
			using var reader = ReadMetadata( animatorFile, animator );
			animator.Tree.SeekField( reader, "m_Controller" );
			return ResolveControllerReference( animatorFile, UnityReference.Read( reader ) );
		}
		return null;
	}

	UnityAssetPointer? ResolveControllerReference( UnityAssetFile file, UnityReference reference )
	{
		if ( (bundles.TryResolve( file, reference, UnityAnimatorController.ClassId, out var owner, out var asset ) ||
			bundles.TryResolve( file, reference, UnityAnimatorController.OverrideClassId, out owner, out asset )) && asset.Tree is not null )
			return new( owner, asset );
		return null;
	}

	UnityAnimationSource[] CollectAnimations( UnityAssetFile file )
	{
		var sources = new List<UnityAnimationSource>();
		var seen = new HashSet<(UnityAssetFile File, long Id)>();
		foreach ( var asset in file.Data.Objects )
		{
			if ( asset.ClassId == UnityAnimationClip.ClassId )
			{
				sources.Add( new( file, asset ) );
				seen.Add( (file, asset.Id) );
			}
		}

		foreach ( var controller in file.Data.Objects.Where( asset => asset.ClassId == UnityAnimatorController.ClassId && asset.Tree is not null ) )
		{
			foreach ( var clip in GetControllerClips( new( file, controller ) ) )
				if ( seen.Add( (clip.File, clip.Asset.Id) ) ) sources.Add( clip );
		}

		return sources.ToArray();
	}

	UnityAnimationSource[] GetControllerClips( UnityAssetPointer controller ) => GetControllerAnimations( controller ).Clips;

	UnityControllerAnimations GetControllerAnimations( UnityAssetPointer controller ) =>
		_controllerAnimations.TryGetValue( controller, out var animations ) ? animations :
		_controllerAnimations[controller] = ReadControllerAnimations( controller.File, controller.Asset );

	UnityControllerAnimations ReadControllerAnimations( UnityAssetFile file, SerializedFile.Object controller )
	{
		UnityAnimationSource?[] slots = [];
		UnityBlendTreeData[] blendTrees = [];
		UnityAnimatorControllerData graph = null;
		try
		{
			var replacements = new Dictionary<UnityAnimationSource, UnityAnimationSource>();
			var visited = new HashSet<UnityAssetPointer>();
			while ( controller.ClassId == UnityAnimatorController.OverrideClassId )
			{
				if ( visited.Count >= 32 || !visited.Add( new( file, controller ) ) || controller.Tree is null )
					throw new InvalidDataException( "Invalid Unity animator override controller hierarchy." );
				using var data = ReadMetadata( file, controller );
				var (baseController, overrides) = UnityAnimatorController.ReadOverrides( data, controller.Tree );
				foreach ( var (original, replacement) in overrides )
					if ( ResolveClip( original ) is { } key && ResolveClip( replacement ) is { } value )
						replacements.TryAdd( key, value );

				if ( ResolveControllerReference( file, baseController ) is not { } source ) return new( [], [], null );
				(file, controller) = (source.File, source.Asset);
			}

			using var reader = ReadMetadata( file, controller );
			var references = UnityAnimatorController.ReadClips( reader, controller.Tree );
			slots = new UnityAnimationSource?[references.Length];
			for ( var i = 0; i < references.Length; i++ )
				if ( ResolveClip( references[i] ) is { } clip )
					slots[i] = replacements.GetValueOrDefault( clip, clip );

			try
			{
				(blendTrees, graph) = UnityAnimatorGraph.Read( reader, controller.Tree );
			}
			catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
			{
				Log.Warning( $"Could not read Unity animation graph for controller {controller.Id} in '{file.Entry.Name}': {e.Message}" );
			}
		}
		catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
		{
			Log.Warning( $"Could not index Unity animator controller {controller.Id} in '{file.Entry.Name}': {e.Message}" );
		}
		return new( slots, blendTrees, graph );

		UnityAnimationSource? ResolveClip( UnityReference reference ) =>
			bundles.TryResolve( file, reference, UnityAnimationClip.ClassId, out var owner, out var asset ) ? new( owner, asset ) : null;
	}

	UnitySkin.Joint[] ResolveJoints( UnityAssetPointer renderer, UnityReference[] references, UnityAssetPointer mesh, out UnityPrefabJoint[] prefabJoints )
	{
		prefabJoints = [];
		var transform = FindRendererTransform( renderer );
		var animator = transform is { } root ? FindAnimator( root ) : null;
		if ( references.Length > 0 )
		{
			var aliases = animator is { } explicitAnimator ? ReadAvatarPathAliases( explicitAnimator ) : null;
			var hashes = aliases is { Count: > 0 } ? ReadMeshBoneHashes( mesh ) : [];
			return ResolveJoints( renderer, transform, references, hashes, aliases, out prefabJoints );
		}
		return animator is { } optimizedAnimator && FindAvatarReference( optimizedAnimator, requireOptimized: true ) is { } avatar
			? ReadAvatar( avatar ).ResolveJoints( ReadMeshBoneHashes( mesh ) ) : null;
	}

	static uint[] ReadMeshBoneHashes( UnityAssetPointer mesh )
	{
		if ( !mesh.Asset.Tree.HasField( "m_BoneNameHashes" ) ) return [];
		using var reader = ReadMetadata( mesh.File, mesh.Asset );
		mesh.Asset.Tree.SeekField( reader, "m_BoneNameHashes" );
		return UnityAvatar.ReadHashes( reader, UnitySkin.MaxBones );
	}

	UnityAssetPointer? FindAnimator( UnityAssetPointer transform )
	{
		for ( var depth = 0; depth <= 128; depth++ )
		{
			var source = ReadTransform( transform.File, transform.Asset );
			if ( !bundles.TryResolve( transform.File, source.Owner, UnitySceneObjects.GameObjectClassId, out var ownerFile, out var owner ) ||
				owner.Tree is null ) return null;
			foreach ( var component in ReadGameObject( ownerFile, owner ).Components )
			{
				if ( !bundles.TryResolve( ownerFile, component, UnityAvatar.AnimatorClassId, out var animatorFile, out var animator ) ||
					animator.Tree is null ) continue;
				return new( animatorFile, animator );
			}
			if ( !bundles.TryResolve( transform.File, source.Parent, UnitySceneObjects.TransformClassId, out var parentFile, out var parent ) ||
				parent.Tree is null ) return null;
			transform = new( parentFile, parent );
		}
		return null;
	}

	UnityAssetPointer? FindAvatarReference( UnityAssetPointer animator, bool requireOptimized = false )
	{
		var tree = animator.Asset.Tree;
		if ( !tree.HasField( "m_Avatar" ) ) return null;
		using var reader = ReadMetadata( animator.File, animator.Asset );
		if ( requireOptimized )
		{
			tree.SeekField( reader, "m_HasTransformHierarchy" );
			if ( reader.ReadBoolean() ) return null;
			reader.Position = 0;
		}
		tree.SeekField( reader, "m_Avatar" );
		return bundles.TryResolve( animator.File, UnityReference.Read( reader ), UnityAvatar.ClassId, out var file, out var avatar ) &&
			avatar.Tree is not null ? new( file, avatar ) : null;
	}

	UnityAvatar ReadAvatar( UnityAssetPointer avatar )
	{
		if ( _avatars.TryGetValue( avatar, out var cached ) ) return cached;
		using var reader = ReadMetadata( avatar.File, avatar.Asset );
		return _avatars[avatar] = UnityAvatar.Read( reader, avatar.Asset.Tree );
	}

	Dictionary<uint, uint> ReadAvatarPathAliases( UnityAssetPointer animator )
	{
		if ( FindAvatarReference( animator ) is not { } avatar || !avatar.Asset.Tree.HasField( "m_TOS" ) ) return null;
		if ( _avatarPathAliases.TryGetValue( avatar, out var cached ) ) return cached;
		using var reader = ReadMetadata( avatar.File, avatar.Asset );
		return _avatarPathAliases[avatar] = UnityAvatar.ReadPathAliases( reader, avatar.Asset.Tree );
	}

	UnitySkin.Joint[] ResolveJoints( UnityAssetPointer renderer, UnityAssetPointer? meshTransform, UnityReference[] references,
		uint[] meshHashes, Dictionary<uint, uint> aliases, out UnityPrefabJoint[] prefabJoints )
	{
		prefabJoints = [];
		if ( references.Length is <= 0 or > UnitySkin.MaxBones ) return null;
		var jointSlots = new Dictionary<(UnityAssetFile File, long Id), int>();
		var transforms = new UnityAssetPointer[references.Length];
		for ( var i = 0; i < references.Length; i++ )
		{
			if ( !bundles.TryResolve( renderer.File, references[i], UnitySceneObjects.TransformClassId, out var jointFile, out var asset ) ||
				asset.Tree is null || !jointSlots.TryAdd( (jointFile, asset.Id), i ) ) return null;
			transforms[i] = new( jointFile, asset );
		}

		var result = new UnitySkin.Joint[references.Length];
		var names = UnitySkin.MakeBoneNames( transforms.Select( transform => ReadJointTransform( transform.File, transform.Asset ).Name ).ToArray() );
		if ( names is null ) return null;
		var meshWorld = meshTransform is { } mesh ? ReadWorldTransform( mesh ) : null;
		for ( var i = 0; i < transforms.Length; i++ )
		{
			var (jointFile, asset) = transforms[i];
			var transform = ReadJointTransform( jointFile, asset );

			var path = transform.Name;
			var hashes = new List<uint> { UnitySkin.HashPath( path ) };
			var parent = -1;
			var directParent = false;
			var current = transform;
			var visited = new HashSet<(UnityAssetFile File, long Id)> { (jointFile, asset.Id) };
			while ( bundles.TryResolve( jointFile, current.Parent, UnitySceneObjects.TransformClassId, out var parentFile, out var parentAsset ) )
			{
				var key = (parentFile, parentAsset.Id);
				if ( !visited.Add( key ) || parentAsset.Tree is null || visited.Count > 256 ) return null;
				if ( parent < 0 && jointSlots.TryGetValue( key, out var parentSlot ) )
				{
					parent = parentSlot;
					directParent = visited.Count == 2;
				}
				jointFile = parentFile;
				current = ReadJointTransform( parentFile, parentAsset );
				path = $"{current.Name}/{path}";
				hashes.Add( UnitySkin.HashPath( path ) );
			}
			// A removed rig wrapper can leave the Avatar's authored path longer than the prefab path.
			if ( meshHashes.Length == references.Length && hashes.Contains( meshHashes[i] ) &&
				aliases.TryGetValue( meshHashes[i], out var authored ) && !hashes.Contains( authored ) )
				hashes.Insert( 0, authored );
			Transform? rootParentInMesh = null;
			if ( parent < 0 && meshWorld is { } world && ReadWorldTransform( new( transforms[i].File, asset ), includeLocal: false ) is { } ancestor )
				rootParentInMesh = world.ToLocal( ancestor );
			result[i] = new( names[i], parent, directParent, hashes.ToArray(), transform.Local, rootParentInMesh );
		}
		prefabJoints = transforms.Select( ( transform, i ) => new UnityPrefabJoint( transform, names[i] ) ).ToArray();
		return result;
	}

	Transform? ReadWorldTransform( UnityAssetPointer pointer, bool includeLocal = true )
	{
		var transform = ReadJointTransform( pointer.File, pointer.Asset );
		var world = includeLocal ? transform.Local : Transform.Zero;
		for ( var depth = 0; depth < 256; depth++ )
		{
			if ( transform.Parent.Id == 0 ) return world;
			if ( !bundles.TryResolve( pointer.File, transform.Parent, UnitySceneObjects.TransformClassId, out var file, out var asset ) ||
				asset.Tree is null ) return null;
			pointer = new( file, asset );
			transform = ReadJointTransform( file, asset );
			world = transform.Local.ToWorld( world );
		}
		return null;
	}

	JointTransform ReadJointTransform( UnityAssetFile file, SerializedFile.Object asset )
	{
		var key = (file, asset.Id);
		if ( _jointTransforms.TryGetValue( key, out var transform ) ) return transform;
		var source = ReadTransform( file, asset );
		if ( !bundles.TryResolve( file, source.Owner, UnitySceneObjects.GameObjectClassId, out var ownerFile, out var gameObject ) ||
			gameObject.Tree is null ) throw new InvalidDataException( "Unity transform has no GameObject." );

		transform = new( ReadGameObject( ownerFile, gameObject ).Name, source.Parent, source.Local );
		_jointTransforms.Add( key, transform );
		return transform;
	}
}

namespace UnityMount;

static class RiskOfRain2VisualSkins
{
	public static void Read( BundleIndex bundles, UnityAddressedAssets addresses, UnityAssetFile[] files, UnityVisualOverrides result )
	{
		foreach ( var file in files )
		{
			foreach ( var model in file.Data.Objects )
			{
				if ( model.ClassId != SerializedFile.MonoBehaviourClassId || model.Tree?.HasField( "_animatorControllerAddress" ) != true ||
					!model.Tree.HasField( "skins" ) ) continue;
				try
				{
					ReadModel( bundles, addresses, file, model, result );
				}
				catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
				{
					Log.Warning( $"Could not read Risk of Rain 2 model skin {model.Id}: {e.Message}" );
				}
			}
		}
	}

	static void ReadModel( BundleIndex bundles, UnityAddressedAssets addresses, UnityAssetFile file,
		SerializedFile.Object model, UnityVisualOverrides result )
	{
		using var reader = Read( new( file, model ) );
		model.Tree.SeekField( reader, "m_GameObject" );
		var owner = UnityReference.Read( reader );
		reader.Position = 0;
		model.Tree.SeekField( reader, "skins" );
		var skins = UnityReference.ReadArray( reader );
		if ( !bundles.TryResolve( file, owner, UnitySceneObjects.GameObjectClassId, out var ownerFile, out var ownerObject ) ) return;
		var root = new UnityAssetPointer( ownerFile, ownerObject );
		var controller = ReadAsset( bundles, addresses, file, model.Tree, reader,
			"_animatorControllerAddress", "_animatorController", UnityAnimatorController.ClassId ) ??
			ReadAsset( bundles, addresses, file, model.Tree, reader,
				"_animatorControllerAddress", "_animatorController", UnityAnimatorController.OverrideClassId );
		if ( controller is { } source ) result.Controllers.TryAdd( root, source );
		if ( skins.Length == 0 ) return;

		var defaults = new UnityVisualOverrides();
		if ( ReadSkin( bundles, addresses, file, skins[0], root, defaults, new() ) is null ) return;
		foreach ( var (renderer, visual) in defaults.Renderers ) result.Renderers[renderer] = visual;
		foreach ( var (objectId, active) in defaults.Activations ) result.Activations[objectId] = active;
		foreach ( var (objectId, attachments) in defaults.Attachments ) result.Attachments[objectId] = attachments;

		var variants = new List<UnityVisualOverrides.Skin>();
		foreach ( var reference in skins.Skip( 1 ) )
		{
			try
			{
				var visuals = new UnityVisualOverrides();
				var name = ReadSkin( bundles, addresses, file, reference, root, visuals, new() );
				if ( name is not null ) variants.Add( new( root, name, visuals ) );
			}
			catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
			{
				Log.Warning( $"Could not read Risk of Rain 2 skin {reference.Id}: {e.Message}" );
			}
		}
		if ( variants.Count > 0 ) result.Skins.TryAdd( root, variants.ToArray() );
	}

	static string ReadSkin( BundleIndex bundles, UnityAddressedAssets addresses, UnityAssetFile file, UnityReference reference,
		UnityAssetPointer model, UnityVisualOverrides result, HashSet<UnityAssetPointer> ancestors )
	{
		if ( !bundles.TryResolve( file, reference, SerializedFile.MonoBehaviourClassId, out var skinFile, out var skin ) ||
			skin.Tree?.HasField( "skinDefParamsAddress" ) != true ) return null;
		var source = new UnityAssetPointer( skinFile, skin );
		if ( ancestors.Count >= 32 || !ancestors.Add( source ) )
			throw new InvalidDataException( "Invalid Risk of Rain 2 skin inheritance." );

		using var reader = Read( source );
		skin.Tree.SeekField( reader, "rootObject" );
		if ( !bundles.TryResolve( skinFile, UnityReference.Read( reader ), UnitySceneObjects.GameObjectClassId, out var rootFile, out var root ) ) return null;
		var skinRoot = new UnityAssetPointer( rootFile, root );
		var bindings = skinRoot == model ? null : RiskOfRain2SkinBindings.Match( bundles, skinRoot, model );
		if ( bindings is { Count: 0 } ) return null;

		reader.Position = 0;
		skin.Tree.SeekField( reader, "baseSkins" );
		foreach ( var parent in UnityReference.ReadArray( reader ) )
			if ( ReadSkin( bundles, addresses, skinFile, parent, model, result, ancestors ) is null ) return null;
		ancestors.Remove( source );

		var parameters = ReadAsset( bundles, addresses, skinFile, skin.Tree, reader,
			"skinDefParamsAddress", "skinDefParams", SerializedFile.MonoBehaviourClassId );
		if ( parameters is not { } data ) return null;
		ApplySkin( bundles, addresses, data, result, bindings );

		reader.Position = 0;
		skin.Tree.SeekField( reader, "m_Name" );
		return reader.ReadAlignedString();
	}

	static void ApplySkin( BundleIndex bundles, UnityAddressedAssets addresses, UnityAssetPointer parameters,
		UnityVisualOverrides result, Dictionary<UnityAssetPointer, UnityAssetPointer> bindings )
	{
		using var reader = Read( parameters );
		var tree = parameters.Asset.Tree;
		var file = parameters.File;
		Visit( "meshReplacements", (item, start) =>
		{
			if ( ReadRenderer( bundles, file, item, reader ) is not { } source || Bind( source ) is not { } renderer ) return;
			var mesh = ReadAsset( bundles, addresses, file, item, reader, "meshAddress", "mesh", UnityMesh.ClassId, start );
			var previous = result.Renderers.GetValueOrDefault( renderer );
			result.Renderers[renderer] = new( mesh ?? previous?.Mesh, previous?.Material );
		} );
		Visit( "rendererInfos", (item, start) =>
		{
			if ( ReadRenderer( bundles, file, item, reader ) is not { } source || Bind( source ) is not { } renderer ) return;
			var material = ReadAsset( bundles, addresses, file, item, reader, "defaultMaterialAddress", "defaultMaterial", UnityMaterial.ClassId, start );
			var previous = result.Renderers.GetValueOrDefault( renderer );
			result.Renderers[renderer] = new( previous?.Mesh, material ?? previous?.Material );
		} );
		Visit( "gameObjectActivations", (item, start) =>
		{
			item.SeekField( reader, "gameObject" );
			var reference = UnityReference.Read( reader );
			if ( !bundles.TryResolve( file, reference, UnitySceneObjects.GameObjectClassId, out var targetFile, out var target ) ) return;
			if ( Bind( new( targetFile, target ) ) is not { } owner ) return;

			reader.Position = start;
			if ( item.HasField( "spawnPrefabOnModelObject" ) )
			{
				item.SeekField( reader, "spawnPrefabOnModelObject" );
				if ( reader.ReadBoolean() )
				{
					if ( ReadAsset( bundles, addresses, file, item, reader, "prefabAddress", "prefab", UnitySceneObjects.GameObjectClassId, start ) is { } prefab )
						AddAttachment( owner, prefab, item, start );
					return;
				}
				reader.Position = start;
			}
			item.SeekField( reader, "shouldActivate" );
			result.Activations[owner] = reader.ReadBoolean();
		} );

		UnityAssetPointer? Bind( UnityAssetPointer source ) => bindings is null ? source :
			bindings.TryGetValue( source, out var target ) ? target : null;

		void AddAttachment( UnityAssetPointer owner, UnityAssetPointer prefab, UnityTypeTree item, long start )
		{
			reader.Position = start;
			item.SeekField( reader, "localPosition" );
			var position = ReadVector();
			var rotation = ReadVector();
			var scale = ReadVector();
			if ( !float.IsFinite( position.LengthSquared() + rotation.LengthSquared() + scale.LengthSquared() ) ) return;
			var local = new Transform( UnityCoordinates.Position( position ), UnityCoordinates.EulerRotation( rotation ), UnityCoordinates.Scale( scale ) );

			if ( !result.Attachments.TryGetValue( owner, out var attachments ) )
				result.Attachments.Add( owner, attachments = new() );
			var attachment = new UnityVisualOverrides.Attachment( prefab, local );
			if ( !attachments.Contains( attachment ) ) attachments.Add( attachment );
		}

		System.Numerics.Vector3 ReadVector() => new( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );

		void Visit( string field, Action<UnityTypeTree, long> visit )
		{
			reader.Position = 0;
			tree.SeekField( reader, field ).VisitArray( reader, visit );
		}
	}

	static UnityAssetPointer? ReadRenderer( BundleIndex bundles, UnityAssetFile file, UnityTypeTree tree, UnityReader reader )
	{
		tree.SeekField( reader, "renderer" );
		var reference = UnityReference.Read( reader );
		return bundles.TryResolve( file, reference, UnitySkin.RendererClassId, out var target, out var asset ) ||
			bundles.TryResolve( file, reference, UnityMesh.RendererClassId, out target, out asset ) ||
			bundles.TryResolve( file, reference, UnityParticleEmitter.RendererClassId, out target, out asset ) ? new( target, asset ) : null;
	}

	static UnityAssetPointer? ReadAsset( BundleIndex bundles, UnityAddressedAssets addresses, UnityAssetFile file,
		UnityTypeTree tree, UnityReader reader, string addressField, string referenceField, int classId, long start = 0 )
	{
		reader.Position = start;
		tree.SeekField( reader, referenceField );
		if ( bundles.TryResolve( file, UnityReference.Read( reader ), classId, out var target, out var asset ) && asset.Tree is not null )
			return new( target, asset );

		reader.Position = start;
		var address = tree.SeekField( reader, addressField );
		address.SeekField( reader, "m_AssetGUID" );
		var guid = reader.ReadAlignedString();
		var name = reader.ReadAlignedString();
		return addresses.Resolve( guid, name, classId );
	}

	static UnityReader Read( UnityAssetPointer source )
	{
		const int maxSize = 4 * 1024 * 1024;
		if ( source.Asset.Size > maxSize ) throw new InvalidDataException( "Unity skin metadata is too large." );
		return new( source.File.ReadObject( source.Asset, maxSize ), leaveOpen: false );
	}
}

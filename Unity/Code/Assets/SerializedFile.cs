namespace UnityMount;

sealed class SerializedFile
{
	public const int MonoBehaviourClassId = 114;
	public const int MonoScriptClassId = 115;

	public readonly record struct Object( long Id, int ClassId, long Offset, int Size, UnityTypeTree Tree, int TypeIndex );

	const int HeaderSize = 48;
	const int MaxMetadataSize = 32 * 1024 * 1024;

	public string UnityVersion { get; }
	public Object[] Objects { get; }
	public string[] Externals { get; }
	readonly Dictionary<long, Object> _byId;

	SerializedFile( string unityVersion, Object[] objects, string[] externals )
	{
		UnityVersion = unityVersion;
		Objects = objects;
		Externals = externals;
		_byId = new( objects.Length );
		foreach ( var asset in objects )
			if ( !_byId.TryAdd( asset.Id, asset ) ) throw new InvalidDataException( $"Duplicate Unity object ID {asset.Id}." );
	}

	public bool TryGetObject( long id, out Object asset ) => _byId.TryGetValue( id, out asset );

	public static SerializedFile Read( UnityBundle bundle, Stream stream, UnityBundle.Entry entry, UnityTypeSchemas schemas )
	{
		using var headerStream = new MemoryStream( bundle.ReadRange( stream, entry, 0, HeaderSize ), writable: false );
		using var header = new UnityReader( headerStream );
		header.Skip( 8 ); // Legacy metadata and file size.
		var version = header.ReadBigInt32();
		if ( version != 22 ) throw new NotSupportedException( $"Unity serialized file version {version}." );
		header.Skip( 4 ); // Legacy data offset.
		if ( header.ReadByte() != 0 ) throw new NotSupportedException( "Big-endian Unity serialized files." );
		header.Skip( 3 );
		var metadataSize = header.ReadBigInt32();
		var fileSize = header.ReadBigInt64();
		var dataOffset = header.ReadBigInt64();
		if ( metadataSize is <= 0 or > MaxMetadataSize || fileSize != entry.Length ||
			dataOffset < HeaderSize + metadataSize || dataOffset > fileSize )
			throw new InvalidDataException( "Invalid Unity serialized file header." );

		using var metadataStream = new MemoryStream( bundle.ReadRange( stream, entry, HeaderSize, metadataSize ), writable: false );
		using var metadata = new UnityReader( metadataStream );
		var unityVersion = metadata.ReadNullTerminatedString();
		metadata.ReadInt32(); // Build target.
		var hasTypeTree = metadata.ReadBoolean();
		var typeCount = metadata.ReadInt32();
		if ( typeCount < 0 || typeCount > metadata.Remaining / 23 )
			throw new InvalidDataException( "Invalid Unity type count." );

		var types = new (int ClassId, UnityTypeTree Tree)[typeCount];
		for ( var i = 0; i < typeCount; i++ )
		{
			var classId = metadata.ReadInt32();
			types[i].ClassId = classId;
			metadata.Skip( 3 ); // Stripped flag and script index.
			if ( classId == MonoBehaviourClassId ) metadata.Skip( 16 ); // MonoBehaviour script ID.
			var hash = new Guid( metadata.ReadExactBytes( 16 ) );
			if ( !hasTypeTree )
			{
				types[i].Tree = schemas.Find( classId, hash );
				continue;
			}

			types[i].Tree = UnityTypeTree.Read( metadata, classId is UnityMesh.ClassId or UnityMaterial.ClassId or UnityMesh.RendererClassId
				or UnitySkin.RendererClassId or UnitySceneObjects.TransformClassId or UnitySceneObjects.GameObjectClassId or UnitySceneObjects.AssetBundleClassId
				or UnitySceneObjects.LodGroupClassId or UnityBillboard.ClassId or UnityBillboard.RendererClassId
				or UnitySprite.ClassId or UnitySpriteRenderer.ClassId or UnityLine.ClassId
				or UnityRenderSettings.ClassId or UnityShader.ClassId or UnityReflectionProbe.ClassId
				or UnityAnimationClip.ClassId or UnityAnimatorController.ClassId or UnityAnimatorController.OverrideClassId
				or UnityAvatar.AnimatorClassId or UnityAvatar.ClassId or UnityLight.ClassId or UnityTimeline.DirectorClassId
				or UnityParticleEmitter.ClassId or UnityParticleEmitter.RendererClassId or MonoBehaviourClassId or MonoScriptClassId
				or UnityCollider.BoxClassId or UnityCollider.SphereClassId or UnityCollider.CapsuleClassId or UnityCollider.MeshClassId );
			schemas.Add( classId, hash, types[i].Tree );
			var dependencies = metadata.ReadInt32();
			metadata.Skip( 4L * dependencies );
		}

		var objectCount = metadata.ReadInt32();
		if ( objectCount < 0 || objectCount > metadata.Remaining / 24 )
			throw new InvalidDataException( "Invalid Unity object count." );

		var objects = new Object[objectCount];
		for ( var i = 0; i < objectCount; i++ )
		{
			metadata.Align( 4 );
			var id = metadata.ReadInt64();
			var offset = metadata.ReadInt64();
			var size = metadata.ReadInt32();
			var type = metadata.ReadInt32();
			if ( type < 0 || type >= types.Length || offset < 0 || offset > fileSize - dataOffset ||
				size < 0 || size > fileSize - dataOffset - offset )
				throw new InvalidDataException( $"Invalid Unity object record {id}." );
			objects[i] = new( id, types[type].ClassId, dataOffset + offset, size, types[type].Tree, type );
		}

		var scriptCount = metadata.ReadInt32();
		if ( scriptCount < 0 || scriptCount > metadata.Remaining / 12 )
			throw new InvalidDataException( "Invalid Unity script reference count." );
		metadata.Align( 4 );
		metadata.Skip( 12L * scriptCount ); // Local file index and path ID.

		var externalCount = metadata.ReadInt32();
		if ( externalCount < 0 || externalCount > metadata.Remaining / 22 )
			throw new InvalidDataException( "Invalid Unity external reference count." );
		var externals = new string[externalCount];
		for ( var i = 0; i < externals.Length; i++ )
		{
			metadata.ReadNullTerminatedString(); // Legacy asset path.
			metadata.Skip( 20 ); // GUID and reference type.
			externals[i] = metadata.ReadNullTerminatedString();
		}

		return new( unityVersion, objects, externals );
	}
}

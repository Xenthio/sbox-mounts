namespace UnityMount;

sealed partial class UnityResourceRegistry
{
	readonly Dictionary<UnityAssetPointer, UnityParticleEmitter> _particleEmitters = new();
	readonly Dictionary<(UnityAssetPointer Source, ParticleAppearanceUse Use), UnityParticleAppearance?> _particleAppearances = new();
	readonly Dictionary<(UnityAssetPointer Renderer, UnityAssetPointer Surface), UnityParticleMeshAppearance> _particleMeshes = new();
	readonly Dictionary<(string Group, UnityAssetPointer Mesh, UnityAssetPointer Surface), string> _particleModels = new();

	enum ParticleAppearanceUse { Billboard, Trail, Mesh }

	UnityPrefabParticle ReadParticle( UnityAssetFile file, UnityReference[] components, UnityVisualOverrides.Skin skin, UnityAssetPointer transform )
	{
		UnityParticleEmitter emitter = null;
		UnityAssetFile emitterSource = null;
		UnityAssetPointer? appearance = null;
		foreach ( var reference in components )
		{
			if ( bundles.TryResolve( file, reference, UnityParticleEmitter.ClassId, out var emitterFile, out var asset ) && asset.Tree is not null )
			{
				emitterSource = emitterFile;
				emitter = ReadParticleEmitter( new( emitterFile, asset ) );
			}
			else if ( bundles.TryResolve( file, reference, UnityParticleEmitter.RendererClassId, out var rendererFile, out var renderer ) &&
				renderer.Tree is not null ) appearance = new( rendererFile, renderer );
		}

		if ( emitter is null || appearance is not { } source ) return null;
		try
		{
			using var reader = ReadMetadata( source.File, source.Asset );
			var tree = source.Asset.Tree;
			if ( !UnitySceneObjects.ReadComponentEnabled( reader, tree ) ) return null;
			reader.Position = 0;
			tree.SeekField( reader, "m_RenderMode" );
			var mode = reader.ReadUInt16();
			if ( emitter.Rotation3D && mode != 4 ) return null;
			var maxViewportFraction = 0f;
			if ( mode is 0 or 1 && tree.HasField( "m_MaxParticleSize" ) )
			{
				reader.Position = 0;
				tree.SeekField( reader, "m_MaxParticleSize" );
				var size = reader.ReadSingle();
				if ( float.IsFinite( size ) && size > 0f ) maxViewportFraction = size;
			}
			var materials = ReadReferences( reader, tree, "m_Materials" );
			var stretch = mode == 1 ? ReadParticleStretch( reader, tree ) : null;
			var primary = ReadMaterial( 0 ) is { } surface ? ReadPrimary( surface ) : default;
			var variants = new List<UnityParticleRendererVariant>();
			if ( overrides.OmniEffectMaterials.TryGetValue( transform, out var replacements ) )
				foreach ( var replacement in replacements.OrderBy( item => item.Asset.Id ) )
				{
					var visual = ReadPrimary( replacement );
					if ( visual.HasRenderer ) variants.Add( new( replacement, visual ) );
				}

			UnityParticleAppearance? trail = null;
			if ( emitter.Trail is not null && ReadMaterial( 1 ) is { } ribbon &&
				RegisterParticleAppearance( ribbon, ParticleAppearanceUse.Trail ) is { } trailStyle ) trail = trailStyle;

			var planes = emitter.Collision?.Planes?.Select( ResolvePlane ).OfType<UnityAssetPointer>().ToArray();
			var collisionEmitters = emitter.CollisionEmitters.Select( ResolveCollisionEmitter ).OfType<UnityParticleCollisionBinding>().ToArray();
			return !primary.HasRenderer && trail is null && variants.Count == 0 && planes is not { Length: > 0 } && collisionEmitters.Length == 0
				? null : new( emitter, primary, trail, particleGravity, stretch, maxViewportFraction, planes, collisionEmitters, variants.ToArray() );

			UnityParticlePrimaryRenderer ReadPrimary( UnityAssetPointer material )
			{
				if ( mode == 4 ) return new( null, RegisterParticleMesh( source, material, reader ) );
				if ( (mode == 0 || stretch.HasValue) && RegisterParticleAppearance( material, ParticleAppearanceUse.Billboard ) is { } style &&
					(!emitter.Height.HasValue && !stretch.HasValue || style.Material) ) return new( style, null );
				return default;
			}

			UnityParticleCollisionBinding? ResolveCollisionEmitter( UnityParticleSubEmitter sub )
			{
				if ( !bundles.TryResolve( emitterSource, sub.Emitter, UnityParticleEmitter.ClassId, out var targetFile, out var target ) ||
					target.Tree is null ) return null;
				using var metadata = ReadMetadata( targetFile, target );
				target.Tree.SeekField( metadata, "m_GameObject" );
				return bundles.TryResolve( targetFile, UnityReference.Read( metadata ), UnitySceneObjects.GameObjectClassId, out var ownerFile, out var owner ) &&
					owner.Tree is not null && FindTransform( new( ownerFile, owner ) ) is { } node
					? new( node, sub.Probability, sub.InheritRotation ) : null;
			}

			UnityAssetPointer? ResolvePlane( UnityReference reference ) =>
				bundles.TryResolve( emitterSource, reference, UnitySceneObjects.TransformClassId, out var planeFile, out var plane )
					? new( planeFile, plane ) : null;

			UnityAssetPointer? ReadMaterial( int index )
			{
				if ( index == 0 )
				{
					var replacement = skin?.Visuals.Renderers.GetValueOrDefault( source )?.Material ??
						overrides.Renderers.GetValueOrDefault( source )?.Material;
					if ( replacement.HasValue ) return replacement;
				}
				if ( index >= materials.Length || !bundles.TryResolve( source.File, materials[index], UnityMaterial.ClassId,
					out var materialFile, out var material ) || material.Tree is null ) return null;
				return new UnityAssetPointer( materialFile, material );
			}
		}
		catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
		{
			Log.Warning( $"Could not index Unity particle renderer {source.Asset.Id} in '{source.File.Entry.Name}': {e.Message}" );
			return null;
		}
	}

	static UnityParticleStretch? ReadParticleStretch( UnityReader reader, UnityTypeTree tree )
	{
		reader.Position = 0;
		tree.SeekField( reader, "m_CameraVelocityScale" );
		var cameraScale = reader.ReadSingle();
		var velocityScale = reader.ReadSingle();
		var lengthScale = reader.ReadSingle();
		if ( cameraScale != 0f || !float.IsFinite( velocityScale ) || !float.IsFinite( lengthScale ) )
			return null;

		var freeform = ReadFlag( "m_FreeformStretching", false );
		var rotate = ReadFlag( "m_RotateWithStretchDirection", true );

		reader.Position = 0;
		tree.SeekField( reader, "m_Pivot" );
		var pivot = new Vector2( reader.ReadSingle(), reader.ReadSingle() );
		return float.IsFinite( pivot.LengthSquared ) ? new( lengthScale, velocityScale, pivot, freeform, rotate ) : null;

		bool ReadFlag( string field, bool fallback )
		{
			if ( !tree.HasField( field ) ) return fallback;
			reader.Position = 0;
			tree.SeekField( reader, field );
			return reader.ReadBoolean();
		}
	}

	UnityParticleMeshAppearance RegisterParticleMesh( UnityAssetPointer renderer, UnityAssetPointer surface, UnityReader reader )
	{
		var rendererSurface = (renderer, surface);
		if ( _particleMeshes.TryGetValue( rendererSurface, out var cached ) ) return cached;
		_particleMeshes.Add( rendererSurface, null );

		var source = UnityParticleMesh.Read( reader, renderer.Asset.Tree );
		if ( source is null || RegisterParticleAppearance( surface, ParticleAppearanceUse.Mesh ) is not { } appearance ) return null;

		var material = RegisterMaterial( surface.File, surface.Asset );
		var choices = new List<UnityParticleMeshAppearance.Choice>( source.Choices.Length );
		foreach ( var choice in source.Choices )
		{
			if ( choice.Weight == 0 ) continue;
			if ( ResolveMesh( renderer.File, choice.Mesh, null ) is not { } mesh ) return null;
			var key = (renderer.File.Group, mesh, surface);
			if ( !_particleModels.TryGetValue( key, out var path ) )
			{
				path = RegisterModel( $"models/{renderer.File.Group}/particles", mesh.File, mesh.Asset, [material], mesh.Asset.Id );
				if ( path is null ) return null;
				_particleModels.Add( key, path );
			}
			choices.Add( new( path, choice.Weight ) );
		}
		return _particleMeshes[rendererSurface] = new( choices.ToArray(), source.Alignment, source.Pivot, source.Flip, appearance.Opaque );
	}

	UnityParticleEmitter ReadParticleEmitter( UnityAssetPointer source )
	{
		if ( _particleEmitters.TryGetValue( source, out var emitter ) ) return emitter;
		try
		{
			using var reader = ReadMetadata( source.File, source.Asset );
			return _particleEmitters[source] = UnityParticleEmitter.Read( reader, source.Asset.Tree );
		}
		catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
		{
			Log.Warning( $"Could not index Unity particle emitter {source.Asset.Id} in '{source.File.Entry.Name}': {e.Message}" );
			return _particleEmitters[source] = null;
		}
	}

	UnityParticleAppearance? RegisterParticleAppearance( UnityAssetPointer source, ParticleAppearanceUse use )
	{
		var key = (source, use);
		if ( _particleAppearances.TryGetValue( key, out var cached ) ) return cached;
		_particleAppearances.Add( key, null );

		using var reader = ReadMetadata( source.File, source.Asset );
		var material = UnityMaterial.Read( reader, source.Asset.Tree, reference => ReadShader( source.File, reference ),
			identifyShader: true );
		if ( !IsResourceName( material.Name ) ) return null;
		if ( material.Distortion is not null )
		{
			if ( use != ParticleAppearanceUse.Billboard ) return null;
			var distortion = RegisterParsedMaterial( source.File, source.Asset, material );
			return distortion.Path is null ? null : _particleAppearances[key] = new( distortion.Path, Material: true, Refractive: true );
		}
		var opaque = material.OpaqueCloud is not null || (use == ParticleAppearanceUse.Mesh && (material.SimpleOpaque || material.Snow is not null));
		if ( material.Cloud is not null || opaque )
		{
			var mounted = RegisterParsedMaterial( source.File, source.Asset, material );
			if ( mounted.Path is null ) return null;
			return _particleAppearances[key] = new( mounted.Path, Material: true, Opaque: opaque );
		}
		if ( use != ParticleAppearanceUse.Billboard ) return null;

		reader.Position = 0;
		var properties = source.Asset.Tree.SeekField( reader, "m_SavedProperties" );
		properties.SeekField( reader, "m_Floats" );
		var count = reader.ReadInt32();
		if ( count < 0 || count > reader.Remaining / 8 ) throw new InvalidDataException( "Invalid Unity particle material float count." );
		var sourceBlend = -1f;
		var destinationBlend = -1f;
		for ( var i = 0; i < count; i++ )
		{
			var property = reader.ReadAlignedString();
			var value = reader.ReadSingle();
			if ( property == "_SrcBlend" ) sourceBlend = value;
			if ( property == "_DstBlend" ) destinationBlend = value;
		}
		// The built-in legacy additive shader declares its blend state in the pass.
		if ( !material.LegacyAdditiveParticle && (sourceBlend, destinationBlend) is not ((1, 1) or (5, 1) or (5, 10)) )
			return null;

		var texture = RegisterTexture( source.File, material.Diffuse );
		if ( texture is null || ReservePath( $"sprites/{source.File.Group}/{material.Name}", ".sprite", source.Asset.Id ) is not { } path )
			return null;
		Define( ResourceType.GameResource, path, () => new UnityParticleSpriteLoader( texture ) );
		return _particleAppearances[key] = new( ResourcePath( path ), material.LegacyAdditiveParticle || destinationBlend == 1 );
	}
}

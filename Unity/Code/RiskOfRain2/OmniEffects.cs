namespace UnityMount;

readonly record struct UnityOmniElement( UnityAssetPointer? Transform, UnityAssetPointer? Material, bool Emit, float MaximumRadius, float BonusEmission );

static class RiskOfRain2OmniEffects
{
	public static void Read( BundleIndex bundles, UnityAssetFile[] files, UnityVisualOverrides visuals )
	{
		foreach ( var file in files )
			foreach ( var asset in file.Data.Objects )
			{
				if ( asset.ClassId != SerializedFile.MonoBehaviourClassId || asset.Tree?.HasField( "omniEffectGroups" ) != true ) continue;
				try
				{
					using var reader = Open( file, asset );
					var tree = asset.Tree;
					if ( !UnitySceneObjects.ReadComponentEnabled( reader, tree ) ) continue;
					reader.Position = 0;
					tree.SeekField( reader, "m_GameObject" );
					if ( !bundles.TryResolve( file, UnityReference.Read( reader ), UnitySceneObjects.GameObjectClassId, out var ownerFile, out var owner ) )
						continue;
					reader.Position = 0;
					var groups = new List<UnityOmniElement[]>();
					tree.SeekField( reader, "omniEffectGroups" ).VisitArray( reader, (group, start) =>
					{
						var elements = new List<UnityOmniElement>();
						group.SeekField( reader, "omniEffectElements" ).VisitArray( reader, (element, elementStart) =>
						{
							var emitter = FindEmitterTransform( bundles, file, ReadReference( "particleSystem" ) );
							reader.Position = elementStart;
							element.SeekField( reader, "particleSystemEmitParticles" );
							var emit = reader.ReadBoolean();
							reader.Position = elementStart;
							element.SeekField( reader, "maximumValidRadius" );
							var maximum = reader.ReadSingle();
							reader.Position = elementStart;
							element.SeekField( reader, "bonusEmissionPerBonusRadius" );
							var bonus = reader.ReadSingle();
							if ( maximum < 0f || float.IsNaN( maximum ) || !float.IsFinite( bonus ) ) return;
							UnityAssetPointer? material = bundles.TryResolve( file, ReadReference( "particleSystemOverrideMaterial" ), UnityMaterial.ClassId,
								out var materialFile, out var materialAsset ) && materialAsset.Tree is not null
								? new( materialFile, materialAsset ) : null;
							elements.Add( new( emitter, material, emit, maximum, bonus ) );

							UnityReference ReadReference( string name )
							{
								reader.Position = elementStart;
								element.SeekField( reader, name );
								return UnityReference.Read( reader );
							}
						} );
						if ( elements.Count > 0 ) groups.Add( elements.ToArray() );
					} );
					if ( groups.Count == 0 || !visuals.OmniEffects.TryAdd( new( ownerFile, owner ), groups.ToArray() ) ) continue;
					foreach ( var element in groups.SelectMany( group => group ) )
					{
						if ( element.Transform is not { } transform || element.Material is not { } material ) continue;
						if ( !visuals.OmniEffectMaterials.TryGetValue( transform, out var materials ) )
							visuals.OmniEffectMaterials.Add( transform, materials = new() );
						materials.Add( material );
					}
				}
				catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
				{
					Log.Warning( $"Could not read Risk of Rain 2 OmniEffect {asset.Id}: {e.Message}" );
				}
			}
	}

	static UnityAssetPointer? FindEmitterTransform( BundleIndex bundles, UnityAssetFile file, UnityReference reference )
	{
		if ( !bundles.TryResolve( file, reference, UnityParticleEmitter.ClassId, out var emitterFile, out var emitter ) || emitter.Tree is null )
			return null;
		using var reader = Open( emitterFile, emitter );
		emitter.Tree.SeekField( reader, "m_GameObject" );
		if ( !bundles.TryResolve( emitterFile, UnityReference.Read( reader ), UnitySceneObjects.GameObjectClassId,
			out var ownerFile, out var owner ) || owner.Tree is null ) return null;

		using var ownerReader = Open( ownerFile, owner );
		foreach ( var component in UnitySceneObjects.ReadGameObject( ownerReader, owner.Tree ).Components )
			if ( bundles.TryResolve( ownerFile, component, UnitySceneObjects.TransformClassId, out var transformFile, out var transform ) )
				return new( transformFile, transform );
		return null;
	}

	static UnityReader Open( UnityAssetFile file, SerializedFile.Object asset ) =>
		new( file.ReadObject( asset, 4 * 1024 * 1024 ), leaveOpen: false );
}

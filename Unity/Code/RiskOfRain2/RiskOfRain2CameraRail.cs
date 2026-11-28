using NumericsVector = System.Numerics.Vector3;

namespace UnityMount;

static class RiskOfRain2CameraRail
{
	public static UnityCameraRail Read( BundleIndex bundles, UnityAssetFile file, UnityReader reader, UnityTypeTree tree )
	{
		reader.Position = 0;
		tree.SeekField( reader, "normalizedPositionAlongCurve" );
		var position = reader.ReadSingle();
		reader.Position = 0;
		tree.SeekField( reader, "curve" );
		if ( !float.IsFinite( position ) || !Resolve( file, UnityReference.Read( reader ), SerializedFile.MonoBehaviourClassId, out var curve ) ||
			!curve.Asset.Tree.HasField( "controlPoints" ) || !curve.Asset.Tree.HasField( "samplesPerSegment" ) ) return null;

		using var definition = Read( curve );
		curve.Asset.Tree.SeekField( definition, "controlPoints" );
		var controls = UnityReference.ReadArray( definition );
		definition.Position = 0;
		curve.Asset.Tree.SeekField( definition, "samplesPerSegment" );
		var samples = definition.ReadInt32();
		if ( controls.Length < 2 || samples < 2 || (long)controls.Length * samples > 32768 ) return null;

		var targets = new UnityCameraRail.Control[controls.Length];
		for ( var i = 0; i < targets.Length; i++ )
		{
			if ( !Resolve( curve.File, controls[i], SerializedFile.MonoBehaviourClassId, out var control ) ||
				!control.Asset.Tree.HasField( "forwardVelocity" ) || !control.Asset.Tree.HasField( "backwardVelocity" ) ) return null;
			using var values = Read( control );
			control.Asset.Tree.SeekField( values, "m_GameObject" );
			if ( !Resolve( control.File, UnityReference.Read( values ), UnitySceneObjects.GameObjectClassId, out var owner ) ) return null;
			using var objectReader = Read( owner );
			var gameObject = UnitySceneObjects.ReadGameObject( objectReader, owner.Asset.Tree );
			var transform = default( UnityAssetPointer );
			foreach ( var component in gameObject.Components )
				if ( Resolve( owner.File, component, UnitySceneObjects.TransformClassId, out transform ) ) break;
			if ( transform.File is null ) return null;
			targets[i] = new( transform, ReadVelocity( "forwardVelocity" ), ReadVelocity( "backwardVelocity" ) );

			Vector3 ReadVelocity( string name )
			{
				values.Position = 0;
				control.Asset.Tree.SeekField( values, name );
				return UnityCoordinates.Position( new NumericsVector( values.ReadSingle(), values.ReadSingle(), values.ReadSingle() ) );
			}
		}
		return new( targets, samples, position );

		bool Resolve( UnityAssetFile source, UnityReference reference, int classId, out UnityAssetPointer result )
		{
			result = default;
			if ( !bundles.TryResolve( source, reference, classId, out var owner, out var asset ) ||
				asset.Tree is null || asset.Size > 65536 ) return false;
			result = new( owner, asset );
			return true;
		}

		static UnityReader Read( UnityAssetPointer source ) =>
			new( source.File.ReadObject( source.Asset, source.Asset.Size ), leaveOpen: false );
	}
}

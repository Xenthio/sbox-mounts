using NumericsMatrix = System.Numerics.Matrix4x4;
using NumericsQuaternion = System.Numerics.Quaternion;
using NumericsVector = System.Numerics.Vector3;

namespace UnityMount;

sealed partial class UnitySkin
{
	/// <summary>Converts a reflected or sheared Unity bind to the native rigid skeleton.</summary>
	public sealed class AffinePose( Joint[] joints, int[] order, NumericsMatrix[] sourceCorrections, NumericsMatrix[] nativeCorrections )
	{
		static readonly NumericsMatrix Basis = new(
			0, -1, 0, 0,
			0, 0, 1, 0,
			1, 0, 0, 0,
			0, 0, 0, 1 );

		readonly Transform[] _local = new Transform[joints.Length];
		readonly NumericsMatrix[] _sourceModel = new NumericsMatrix[joints.Length];
		readonly NumericsMatrix[] _nativeInverse = new NumericsMatrix[joints.Length];

		public static AffinePose Create( Joint[] joints, NumericsMatrix[] sourceBind, NumericsMatrix[] nativeBind, byte[] boneIndices )
		{
			var sourceCorrections = new NumericsMatrix[joints.Length];
			var nativeCorrections = new NumericsMatrix[joints.Length];
			var inverseBind = new NumericsMatrix[joints.Length];
			var sourceInModel = sourceBind.Select( ConvertBasis ).ToArray();
			for ( var i = 0; i < joints.Length; i++ )
				if ( !NumericsMatrix.Invert( sourceInModel[i], out inverseBind[i] ) ) return null;

			for ( var i = 0; i < joints.Length; i++ )
			{
				if ( !NumericsMatrix.Invert( ToMatrix( joints[i].SourceLocal ), out var inverseRest ) ) return null;
				var parent = joints[i].Parent;
				var sourceLocal = parent < 0 ? sourceInModel[i] : sourceInModel[i] * inverseBind[parent];
				// Anchor the authored local motion to the full bind matrix, including missing intermediate joints.
				sourceCorrections[i] = inverseRest * sourceLocal;
				nativeCorrections[i] = ConvertBasis( nativeBind[i] ) * inverseBind[i];
			}

			var order = Enumerable.Range( 0, joints.Length ).OrderBy( i => boneIndices[i] ).ToArray();
			return new( joints, order, sourceCorrections, nativeCorrections );
		}

		public void Sample( UnityAnimationClip clip, (int Joint, UnityAnimationClip.Binding Binding)[] tracks, float time,
			Transform[] frame, Transform[] rigidBind, bool referencePose = false )
		{
			for ( var i = 0; i < joints.Length; i++ ) _local[i] = joints[i].SourceLocal;
			foreach ( var (joint, track) in tracks )
			{
				ref var local = ref _local[joint];
				switch ( track.Attribute )
				{
					case 1: local.Position = UnityCoordinates.Position( clip.SampleVector( track.Channel, time, referencePose ) ); break;
					case 2 or 4: local.Rotation = UnityCoordinates.RotationFor( clip.SampleRotation( track, time, referencePose ) ); break;
					case 3: local.Scale = UnityCoordinates.Scale( clip.SampleVector( track.Channel, time, referencePose ) ); break;
				}
			}

			for ( var slot = 0; slot < order.Length; slot++ )
			{
				var i = order[slot];
				var parent = joints[i].Parent;
				var sourceLocal = ToMatrix( _local[i] ) * sourceCorrections[i];
				_sourceModel[i] = parent < 0 ? sourceLocal : sourceLocal * _sourceModel[parent];
				// Native bind * source inverse bind * source animated model transform.
				var nativeModel = nativeCorrections[i] * _sourceModel[i];
				var nativeLocal = parent < 0 ? nativeModel : nativeModel * _nativeInverse[parent];
				frame[slot] = TryDecompose( nativeLocal, out var pose ) ? pose : rigidBind[i];
				// Child calculations must use the actual native pose, which may have needed a shear approximation.
				var represented = ToMatrix( frame[slot] );
				if ( NumericsMatrix.Invert( represented, out var inverse ) )
					_nativeInverse[i] = parent < 0 ? inverse : _nativeInverse[parent] * inverse;
				else
					_nativeInverse[i] = parent < 0 ? NumericsMatrix.Identity : _nativeInverse[parent];
			}
		}

		public static bool TryRigidBasis( NumericsMatrix source, out NumericsMatrix rigid )
		{
			rigid = default;
			var x = NumericsVector.Normalize( new( source.M11, source.M12, source.M13 ) );
			var y = new NumericsVector( source.M21, source.M22, source.M23 );
			y = NumericsVector.Normalize( y - NumericsVector.Dot( x, y ) * x );
			var z = NumericsVector.Cross( x, y );
			if ( !float.IsFinite( z.LengthSquared() ) ) return false;
			rigid = new( x.X, x.Y, x.Z, 0, y.X, y.Y, y.Z, 0, z.X, z.Y, z.Z, 0,
				source.M41, source.M42, source.M43, 1 );
			return true;
		}

		public static BBox ConvertBounds( BBox bounds, NumericsMatrix sourceBind, NumericsMatrix nativeBind )
		{
			NumericsMatrix.Invert( ConvertBasis( nativeBind ), out var inverse );
			var frame = ConvertBasis( sourceBind ) * inverse;
			return BBox.FromPoints( bounds.Corners.Select( point =>
			{
				var transformed = NumericsVector.Transform( new( point.x, point.y, point.z ), frame );
				return new Vector3( transformed.X, transformed.Y, transformed.Z );
			} ) );
		}

		static bool TryDecompose( NumericsMatrix matrix, out Transform transform )
		{
			transform = default;
			if ( !float.IsFinite( matrix.Translation.LengthSquared() ) ) return false;
			if ( !NumericsMatrix.Decompose( matrix, out var scale, out var rotation, out _ ) || !float.IsFinite( rotation.LengthSquared() ) )
			{
				if ( !TryRigidBasis( matrix, out var rigid ) ) return false;
				rotation = NumericsQuaternion.CreateFromRotationMatrix( rigid );
				scale = new(
					matrix.M11 * rigid.M11 + matrix.M12 * rigid.M12 + matrix.M13 * rigid.M13,
					matrix.M21 * rigid.M21 + matrix.M22 * rigid.M22 + matrix.M23 * rigid.M23,
					matrix.M31 * rigid.M31 + matrix.M32 * rigid.M32 + matrix.M33 * rigid.M33 );
			}
			if ( !float.IsFinite( scale.LengthSquared() ) ) return false;
			transform = new( new( matrix.M41, matrix.M42, matrix.M43 ), new Rotation( rotation.X, rotation.Y, rotation.Z, rotation.W ),
				new Vector3( scale.X, scale.Y, scale.Z ) );
			return true;
		}

		static NumericsMatrix ToMatrix( Transform transform )
		{
			var rotation = transform.Rotation;
			var matrix = NumericsMatrix.CreateScale( transform.Scale.x, transform.Scale.y, transform.Scale.z ) *
				NumericsMatrix.CreateFromQuaternion( new( rotation.x, rotation.y, rotation.z, rotation.w ) );
			matrix.Translation = new( transform.Position.x, transform.Position.y, transform.Position.z );
			return matrix;
		}

		static NumericsMatrix ConvertBasis( NumericsMatrix source )
		{
			var converted = NumericsMatrix.Transpose( Basis ) * source * Basis;
			converted.Translation *= UnityCoordinates.ToInches( 1 );
			return converted;
		}
	}
}

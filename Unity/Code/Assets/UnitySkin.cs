using NumericsMatrix = System.Numerics.Matrix4x4;

namespace UnityMount;

sealed partial class UnitySkin( UnitySkin.Joint[] joints, UnitySkin.VisibilityBounds? visibility = null )
{
	public const int RendererClassId = 137;
	public const int MaxBones = 256;
	internal const string InPlaceSuffix = " (In Place)";

	public readonly record struct Joint( string Name, int Parent, bool DirectParent, uint[] PathHashes, Transform SourceLocal,
		Transform? RootParentInMesh = null );

	public readonly record struct VisibilityBounds( BBox Box, Transform SpaceToMesh, int RootJoint );

	public int Count => joints.Length;
	public BBox? ViewBounds => visibility is { } bounds ? bounds.Box.Transform( bounds.SpaceToMesh ) : null;

	public static BBox? ReadLocalBounds( UnityReader reader, UnityTypeTree tree )
	{
		if ( !tree.HasField( "m_AABB" ) ) return null;

		reader.Position = 0;
		tree.SeekField( reader, "m_AABB" );
		var center = UnityCoordinates.Position( new( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() ) );
		var extent = UnityCoordinates.ToInches( UnityCoordinates.Scale( new( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() ) ) );
		if ( !float.IsFinite( center.LengthSquared ) || !float.IsFinite( extent.LengthSquared ) ||
			extent.x < 0 || extent.y < 0 || extent.z < 0 || extent.LengthSquared == 0 ) return null;

		return new( center - extent, center + extent );
	}

	public static string[] MakeBoneNames( string[] source )
	{
		var original = source.ToHashSet( StringComparer.OrdinalIgnoreCase );
		var used = new HashSet<string>( original.Comparer );
		var names = new string[source.Length];
		for ( var i = 0; i < source.Length; i++ )
		{
			var name = source[i];
			if ( string.IsNullOrWhiteSpace( name ) ) return null;
			if ( !used.Add( name ) )
			{
				// Native bones need unique names. Unity animation paths retain the source names.
				var suffix = 1;
				do { name = $"{source[i]}_{suffix++}"; }
				while ( original.Contains( name ) || !used.Add( name ) );
			}
			names[i] = name;
		}
		return names;
	}

	public static uint HashPath( string path ) => Sandbox.Utility.Crc32.FromBytes( System.Text.Encoding.UTF8.GetBytes( path ) );

	public int[] RemapMeshBones( uint[] hashes )
	{
		var remap = new int[hashes.Length];
		Array.Fill( remap, -1 );
		var mapped = new HashSet<int>();
		for ( var i = 0; i < hashes.Length; i++ )
		{
			var hash = hashes[i];
			var first = Array.FindIndex( joints, joint => joint.PathHashes.Contains( hash ) );
			if ( first < 0 ) continue;
			if ( Array.FindIndex( joints, first + 1, joint => joint.PathHashes.Contains( hash ) ) >= 0 ||
				!mapped.Add( first ) ) return null;
			remap[i] = first;
		}
		return mapped.Count == joints.Length ? remap : null;
	}

	public bool TryBuild( NumericsMatrix[] bindPoses, BBox?[] bindBounds, ModelBuilder builder, out byte[] boneIndices,
		out Transform[] bindTransforms, out float[] parentScales, out AffinePose affine, out Transform[] modelBindPose )
	{
		boneIndices = new byte[joints.Length];
		bindTransforms = new Transform[joints.Length];
		modelBindPose = new Transform[joints.Length];
		parentScales = new float[joints.Length];
		affine = null;
		if ( bindPoses.Length != joints.Length ) return false;

		// Unity stores model-to-bone bind poses. Their inverses position the bones in mesh space.
		var boneToModel = new NumericsMatrix[joints.Length];
		var bindScales = new Vector3[joints.Length];
		var sourceBind = new NumericsMatrix[joints.Length];
		var hasAffineBind = false;
		for ( var i = 0; i < joints.Length; i++ )
		{
			if ( !NumericsMatrix.Invert( bindPoses[i], out var pose ) ||
				!float.IsFinite( pose.Translation.LengthSquared() ) ) return false;
			sourceBind[i] = pose;
			if ( NumericsMatrix.Decompose( pose, out var scale, out var rotation, out _ ) &&
				scale.X > 0 && scale.Y > 0 && scale.Z > 0 && float.IsFinite( rotation.LengthSquared() ) )
			{
				boneToModel[i] = NumericsMatrix.CreateFromQuaternion( rotation );
				bindScales[i] = UnityCoordinates.Scale( scale );
			}
			else
			{
				if ( !AffinePose.TryRigidBasis( pose, out boneToModel[i] ) ) return false;
				hasAffineBind = true;
			}

			// s&box binds rigid bone transforms. Keep model-space positions when removing scale.
			boneToModel[i].Translation = pose.Translation;
		}

		var pending = new List<ModelBuilder.Bone>( joints.Length );
		var emitted = new bool[joints.Length];
		for ( var i = 0; i < joints.Length; i++ )
		{
			var previousCount = pending.Count;
			for ( var jointIndex = 0; jointIndex < joints.Length; jointIndex++ )
			{
				var joint = joints[jointIndex];
				if ( emitted[jointIndex] || joint.Parent >= 0 && !emitted[joint.Parent] ) continue;

				var local = boneToModel[jointIndex];
				if ( joint.Parent >= 0 )
				{
					if ( !NumericsMatrix.Invert( boneToModel[joint.Parent], out var parentInverse ) ) return false;
					local *= parentInverse;
				}
				var rotation = System.Numerics.Quaternion.CreateFromRotationMatrix( local );

				var transform = new Transform( UnityCoordinates.Position( local.Translation ), UnityCoordinates.RotationFor( rotation ) );
				boneIndices[jointIndex] = (byte)pending.Count;
				bindTransforms[jointIndex] = transform;
				var parentScale = bindScales[joint.Parent < 0 ? jointIndex : joint.Parent];
				parentScales[jointIndex] = (parentScale.x + parentScale.y + parentScale.z) / 3;
				// The native builder derives local bind transforms from model-space bones.
				var modelPose = boneToModel[jointIndex];
				var boneInMesh = new Transform( UnityCoordinates.Position( modelPose.Translation ),
					UnityCoordinates.RotationFor( System.Numerics.Quaternion.CreateFromRotationMatrix( modelPose ) ) );
				modelBindPose[boneIndices[jointIndex]] = boneInMesh;
				BBox? boneBounds = null;
				if ( jointIndex < bindBounds.Length && bindBounds[jointIndex] is { } weightedBounds )
				{
					// Native bind bones are rigid, so carry the removed bind scale into the box.
					if ( hasAffineBind )
						boneBounds = AffinePose.ConvertBounds( weightedBounds, sourceBind[jointIndex], boneToModel[jointIndex] );
					else
					{
						var scale = bindScales[jointIndex];
						boneBounds = new( weightedBounds.Mins * scale, weightedBounds.Maxs * scale );
					}
				}
				if ( visibility is { } bounds && bounds.RootJoint == jointIndex )
				{
					var authoredBounds = bounds.Box.Transform( boneInMesh.ToLocal( bounds.SpaceToMesh ) );
					boneBounds = boneBounds?.AddBBox( authoredBounds ) ?? authoredBounds;
				}
				pending.Add( new ModelBuilder.Bone( joint.Name, joint.Parent < 0 ? null : joints[joint.Parent].Name,
					boneInMesh.Position, boneInMesh.Rotation ) { Bounds = boneBounds } );
				emitted[jointIndex] = true;
			}

			if ( pending.Count == joints.Length ) break;
			if ( pending.Count == previousCount ) return false;
		}
		if ( hasAffineBind && (affine = AffinePose.Create( joints, sourceBind, boneToModel, boneIndices )) is null ) return false;
		builder.AddBones( pending.ToArray() );
		return true;
	}

	public void AddAnimations( ModelBuilder builder, UnityAnimationSource[] clips, byte[] indices, Transform[] bindTransforms,
		float[] parentScales, AffinePose affine,
		Func<UnityAssetFile, UnityReference, UnityAnimationEventPrefab> findEventPrefab, UnityControllerAnimations controller,
		IReadOnlyDictionary<string, string> inPlaceAnimations )
	{
		if ( clips.Length == 0 ) return;

		var paths = new Dictionary<uint, int>();
		var ambiguous = new HashSet<uint>();
		for ( var i = 0; i < joints.Length; i++ )
		{
			foreach ( var path in joints[i].PathHashes )
			{
				if ( paths.TryGetValue( path, out var other ) && other != i ) ambiguous.Add( path );
				else paths[path] = i;
			}
		}
		foreach ( var hash in ambiguous ) paths.Remove( hash );

		var frame = new Transform[joints.Length];
		var corrections = new Rotation[joints.Length];
		var referencePose = new Transform[joints.Length];
		for ( var i = 0; i < joints.Length; i++ )
		{
			corrections[i] = joints[i].DirectParent ? Rotation.Identity : bindTransforms[i].Rotation * joints[i].SourceLocal.Rotation.Inverse;
			var joint = joints[i];
			referencePose[i] = joint.RootParentInMesh is { } root
				? root.ToWorld( joint.SourceLocal ).WithScale( 1 )
				: bindTransforms[i];
		}

		var events = new List<UnityAnimationEventData>();
		var rootMotion = new List<UnityRootMotionData>();
		var parameterCurves = new List<UnityAnimatorParameterCurve>();
		var additivePoses = new List<UnityAdditivePoseData>();
		var parameterNames = new Dictionary<uint, string>();
		if ( controller?.Graph is { } graph )
			foreach ( var parameter in graph.Parameters )
				if ( parameter.Name is { } name ) parameterNames.TryAdd( HashPath( name ), name );
		var mounted = controller?.Graph is not null ? new Dictionary<UnityAnimationSource, UnityBlendTreeData.SequenceInfo>() : null;
		var controllerClips = mounted is not null ? controller.Clips.ToHashSet() : [];
		foreach ( var source in clips )
		{
			if ( source.File.ReadAnimation( source.Asset ) is not { } clip ) continue;
			var tracks = clip.Bind( paths );
			if ( tracks.Length == 0 )
			{
				if ( !controllerClips.Contains( source ) ) continue;
				mounted.TryAdd( source, new( null, clip.CycleOffset, clip.Looping, new( clip.Name, clip.Duration ) ) );
			}
			else mounted?.TryAdd( source, new( clip.Name, clip.CycleOffset, clip.Looping ) );

			foreach ( var entry in clip.Events )
			{
				var prefab = findEventPrefab?.Invoke( source.File, entry.Object ) ?? default;
				events.Add( new UnityAnimationEventData
				{
					Sequence = clip.Name,
					Time = entry.Time,
					ClipPhase = clip.Duration > 0 ? entry.Time / clip.Duration : null,
					Function = entry.Function,
					Data = entry.Data,
					Prefab = prefab.Path,
					AlignToSocket = prefab.Attachment.AlignToSocket,
					FollowsSocket = prefab.Attachment.FollowsSocket,
					FloatParameter = entry.FloatParameter,
					IntParameter = entry.IntParameter,
					MessageOptions = entry.MessageOptions
				} );
			}

			if ( UnityRootMotionData.Bake( clip ) is { } motion ) rootMotion.Add( motion );
			foreach ( var binding in clip.ParameterBindings )
				if ( parameterNames.TryGetValue( binding.Hash, out var parameter ) &&
					clip.BindParameterCurve( binding, parameter ) is { } curve ) parameterCurves.Add( curve );
			if ( tracks.Length == 0 ) continue;
			var animation = builder.AddAnimation( clip.Name, clip.Rate ).WithLooping( clip.Looping );
			AnimationBuilder inPlace = null;
			var rootIndex = 0;
			var rootStart = Vector3.Zero;
			var travelAxis = Vector3.Zero;
			if ( inPlaceAnimations?.GetValueOrDefault( clip.Name ) is { } motionBone &&
				Array.FindIndex( joints, joint => joint.Name == motionBone ) is >= 0 and var rootJoint )
			{
				rootIndex = indices[rootJoint];
				SamplePose( clip, tracks, clip.TimeAtFrame( 0 ) );
				rootStart = frame[rootIndex].Position;
				SamplePose( clip, tracks, clip.TimeAtFrame( clip.FrameCount - 1 ) );
				travelAxis = (frame[rootIndex].Position - rootStart).Normal;
				inPlace = builder.AddAnimation( clip.Name + InPlaceSuffix, clip.Rate ).WithLooping( clip.Looping );
			}
			for ( var sample = 0; sample < clip.FrameCount; sample++ )
			{
				SamplePose( clip, tracks, clip.TimeAtFrame( sample ) );
				animation.AddFrame( frame );
				if ( inPlace is null ) continue;

				// Remove travel along the clip's stride while preserving motion across that axis.
				ref var root = ref frame[rootIndex];
				root.Position -= travelAxis * Vector3.Dot( root.Position - rootStart, travelAxis );
				inPlace.AddFrame( frame );
			}
			if ( clip.HasAdditiveReference )
			{
				SamplePose( clip, tracks, 0, reference: true );
				additivePoses.Add( new() { Sequence = animation.Name, Bones = frame.ToArray() } );
			}
		}
		if ( events.Count > 0 ) builder.WithData( events.ToArray() );
		if ( rootMotion.Count > 0 ) builder.WithData( rootMotion.ToArray() );
		if ( parameterCurves.Count > 0 ) builder.WithData( parameterCurves.ToArray() );
		if ( additivePoses.Count > 0 ) builder.WithData( additivePoses.ToArray() );
		if ( mounted is { Count: > 0 } )
		{
			var sequences = controller.Slots.Select( slot =>
				slot is { } source && mounted.TryGetValue( source, out var sequence ) ? (UnityBlendTreeData.SequenceInfo?)sequence : null ).ToArray();
			var blendTrees = controller.BlendTrees.Select( tree => tree.Bind( sequences ) ).OfType<UnityBlendTreeData>().ToArray();
			if ( blendTrees.Length > 0 ) builder.WithData( blendTrees );
			builder.WithData( controller.Graph.Bind( sequences, joints, indices ) );
		}

		void SamplePose( UnityAnimationClip clip, (int Joint, UnityAnimationClip.Binding Binding)[] tracks, float time, bool reference = false )
		{
			if ( affine is not null )
			{
				affine.Sample( clip, tracks, time, frame, bindTransforms, reference );
				return;
			}
			for ( var i = 0; i < joints.Length; i++ ) frame[indices[i]] = referencePose[i];
			foreach ( var (joint, track) in tracks )
			{
				ref var transform = ref frame[indices[joint]];
				var rest = joints[joint].SourceLocal;
				var direct = joints[joint].DirectParent;
				var root = joints[joint].RootParentInMesh;
				switch ( track.Attribute )
				{
					case 1:
						var position = UnityCoordinates.Position( clip.SampleVector( track.Channel, time, reference ) );
						if ( float.IsFinite( position.LengthSquared ) )
							transform.Position = root is { } parent ? parent.PointToWorld( position )
								: direct ? position * parentScales[joint]
								: bindTransforms[joint].Position + corrections[joint] * ((position - rest.Position) * parentScales[joint]);
						break;
					case 2:
					case 4:
						var rotation = clip.SampleRotation( track, time, reference );
						if ( float.IsFinite( rotation.LengthSquared() ) )
							transform.Rotation = (root?.Rotation ?? corrections[joint]) * UnityCoordinates.RotationFor( rotation );
						break;
					case 3:
						var scale = UnityCoordinates.Scale( clip.SampleVector( track.Channel, time, reference ) );
						if ( float.IsFinite( scale.LengthSquared ) && rest.Scale.x != 0 && rest.Scale.y != 0 && rest.Scale.z != 0 )
							transform.Scale = scale / rest.Scale;
						break;
				}
			}
		}
	}

	public static NumericsMatrix ReadBindPose( UnityReader reader )
	{
		// Unity serializes e00, e01... in row order for column vectors. System.Numerics uses row vectors.
		var matrix = new NumericsMatrix(
			reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
			reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
			reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
			reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
		return NumericsMatrix.Transpose( matrix );
	}
}

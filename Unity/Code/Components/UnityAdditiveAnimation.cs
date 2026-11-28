namespace UnityMount;

/// <summary>Composes mounted additive clips over a renderer's current native animation.</summary>
sealed class UnityAdditiveAnimation : IDisposable
{
	internal readonly record struct Layer( int ClipIndex, float Time, float Weight );
	sealed record Clip( SceneModel Sampler, Transform[] Reference );

	readonly SkinnedModelRenderer _renderer;
	readonly SceneModel _target;
	readonly Model _model;
	readonly Clip[] _clips;
	readonly Transform[] _pose;
	readonly BoneCollection.Bone[] _bones;
	bool _applied;

	UnityAdditiveAnimation( SkinnedModelRenderer renderer, string[] sequences )
	{
		_renderer = renderer;
		_target = renderer.SceneModel;
		_model = renderer.Model;
		_bones = _model.Bones.AllBones.OrderBy( Depth ).ToArray();
		_pose = new Transform[_model.BoneCount];
		var references = _model.GetData<UnityAdditivePoseData[]>() ?? [];
		_clips = sequences.Select( sequence =>
		{
			var sampler = new SceneModel( renderer.Scene.SceneWorld, _model, Transform.Zero ) { RenderingEnabled = false };
			sampler.CurrentSequence.Name = sequence;
			sampler.Update( 0 );
			var reference = references.FirstOrDefault( pose => pose.Sequence == sequence && pose.Bones.Length == _model.BoneCount )?.Bones;
			return new Clip( sampler, reference ?? Enumerable.Range( 0, _model.BoneCount ).Select( sampler.GetParentSpaceBone ).ToArray() );
		} ).ToArray();

		static int Depth( BoneCollection.Bone bone )
		{
			var depth = 0;
			while ( (bone = bone.Parent) is not null ) depth++;
			return depth;
		}
	}

	public static UnityAdditiveAnimation Create( SkinnedModelRenderer renderer, params string[] sequences ) =>
		renderer.SceneModel.IsValid() && sequences.All( renderer.Model.AnimationNames.Contains )
			? new( renderer, sequences ) : null;

	public static void ApplyGesture( ref UnityAdditiveAnimation animation, SkinnedModelRenderer renderer,
		string[] sequences, int clipIndex, float phase )
	{
		if ( Application.IsDedicatedServer ) return;
		if ( clipIndex < 0 )
		{
			animation?.Clear();
			return;
		}

		if ( animation is { IsBound: false } )
		{
			animation.Dispose();
			animation = null;
		}
		animation ??= Create( renderer, sequences );
		if ( animation is not null )
			animation.Apply( [new( clipIndex, Math.Clamp( phase, 0f, 1f ) * animation.Duration( clipIndex ), 1f )] );
	}

	public bool IsBound => _renderer.IsValid() && _target.IsValid() &&
		_renderer.SceneModel == _target && _model == _renderer.Model;

	public float Duration( int clipIndex ) => _clips[clipIndex].Sampler.CurrentSequence.Duration;

	public void Apply( float time, float weight ) => Apply( [ new( 0, time, weight ) ] );

	public void Apply( ReadOnlySpan<Layer> layers, ReadOnlySpan<Layer> baseClips = default )
	{
		if ( !IsBound || !_applied && _target.HasBoneOverrides() ) return;
		if ( layers.IsEmpty && baseClips.IsEmpty )
		{
			Clear();
			return;
		}
		if ( _applied )
		{
			_target.ClearBoneOverrides();
			_target.Update( 0 );
		}
		foreach ( var clip in baseClips ) Sample( clip );
		foreach ( var layer in layers ) Sample( layer );

		foreach ( var bone in _bones )
		{
			var index = bone.Index;
			var local = _target.GetParentSpaceBone( index );
			if ( !baseClips.IsEmpty )
			{
				var pose = new UnityPoseSum();
				foreach ( var clip in baseClips )
					pose.Add( _clips[clip.ClipIndex].Sampler.GetParentSpaceBone( index ), clip.Weight );
				local = pose.Resolve();
			}
			foreach ( var layer in layers )
			{
				var clip = _clips[layer.ClipIndex];
				var delta = UnityAdditivePoseData.Delta( clip.Reference[index], clip.Sampler.GetParentSpaceBone( index ) );
				local = UnityAdditivePoseData.Apply( local, delta, layer.Weight );
			}
			_pose[index] = bone.Parent is { } parent ? _pose[parent.Index].ToWorld( local ) : local;
		}
		foreach ( var bone in _bones )
			_target.SetBoneOverride( bone.Index, _pose[bone.Index] );
		_applied = true;
		UpdateModels();
	}

	void Sample( Layer layer )
	{
		var sampler = _clips[layer.ClipIndex].Sampler;
		sampler.CurrentSequence.Time = Math.Clamp( layer.Time, 0f, sampler.CurrentSequence.Duration );
		sampler.Update( 0 );
	}

	public void Clear()
	{
		if ( !_applied ) return;
		_applied = false;
		if ( !_target.IsValid() ) return;
		_target.ClearBoneOverrides();
		UpdateModels();
	}

	void UpdateModels()
	{
		_target.Update( 0 );
		if ( !_renderer.IsValid() ) return;
		foreach ( var part in _renderer.GetComponentsInChildren<SkinnedModelRenderer>() )
			if ( part.BoneMergeTarget == _renderer && part.SceneModel.IsValid() )
				part.SceneModel.MergeBones( _target );
	}

	public void Dispose()
	{
		Clear();
		foreach ( var clip in _clips )
			if ( clip.Sampler.IsValid() ) clip.Sampler.Delete();
	}
}

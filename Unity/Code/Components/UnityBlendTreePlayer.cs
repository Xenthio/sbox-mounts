namespace UnityMount;

/// <summary>Samples a mounted Animator blend tree on a native skinned model.</summary>
[Title( "Mounted Animator Blend Tree" )]
public sealed partial class UnityBlendTreePlayer : Component, Component.DontExecuteOnServer
{
	[Property] public SkinnedModelRenderer Renderer { get; set; }

	public string StatePath => _body?.StatePath ?? _tree?.StatePath;

	UnityBlendTreeData _tree;
	Model _model;
	readonly Dictionary<string, SceneModel> _samplers = new();
	SceneModel _target;
	bool _ownsOverrides;
	int[] _boneOrder = [];
	int[] _parents = [];
	Transform[] _modelPose = [];
	Transform[] _composedPose = [];
	Transform[] _overlayPose = [];
	UnityPoseSum[] _localPose = [];
	UnityPoseSum[] _nextLocalPose = [];

	/// <summary>Selects a state on the assigned model without changing its native animation sequence.</summary>
	public bool SelectState( string statePath, int treeIndex = 0 )
	{
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() || Renderer.BoneMergeTarget.IsValid() ||
			Renderer.Model.GetData<UnityBlendTreeData[]>()?.FirstOrDefault( item =>
				item.StatePath == statePath && item.TreeIndex == treeIndex ) is not { } tree ) return false;

		if ( !PrepareRenderer() ) return false;
		_tree = tree;
		return true;
	}

	bool PrepareRenderer()
	{
		Stop();
		if ( Renderer.SceneModel.HasBoneOverrides() ) return false;
		_target = Renderer.SceneModel;
		_model = Renderer.Model;
		_parents = new int[_model.BoneCount];
		foreach ( var bone in _model.Bones.AllBones )
			_parents[bone.Index] = bone.Parent?.Index ?? -1;
		_boneOrder = _model.Bones.AllBones.OrderBy( Depth ).Select( bone => bone.Index ).ToArray();
		_modelPose = new Transform[_model.BoneCount];
		_composedPose = new Transform[_model.BoneCount];
		_overlayPose = new Transform[_model.BoneCount];
		_localPose = new UnityPoseSum[_model.BoneCount];
		_nextLocalPose = new UnityPoseSum[_model.BoneCount];
		return true;

		static int Depth( BoneCollection.Bone bone )
		{
			var depth = 0;
			while ( (bone = bone.Parent) is not null ) depth++;
			return depth;
		}
	}

	/// <summary>Applies the blended pose at the given normalized state phase and parameter values.</summary>
	public bool Seek( float phase, IReadOnlyDictionary<string, float> parameters )
	{
		if ( _tree is null || !float.IsFinite( phase ) || parameters is null ) return false;
		if ( !CanRender() || UnityBlendWeights.Evaluate( _tree, parameters ) is not { } weights ) return false;
		return RenderPose( EvaluatePose( _tree, weights, phase ) );
	}

	bool CanRender()
	{
		if ( !_target.IsValid() || !Renderer.IsValid() || Renderer.SceneModel != _target ||
			Renderer.Model != _model || Renderer.BoneMergeTarget.IsValid() )
		{
			Stop();
			return false;
		}
		return _ownsOverrides || !_target.HasBoneOverrides();
	}

	readonly record struct Pose( UnityBlendTreeData Tree, float[] Weights, float Phase, float BoneWeight );

	static Pose EvaluatePose( UnityBlendTreeData tree, float[] weights, float phase )
	{
		var boneWeight = 0f;
		for ( var i = 0; i < weights.Length; i++ )
			if ( tree.Nodes[i].Sequence is not null ) boneWeight += weights[i];
		return new( tree, weights, phase, boneWeight );
	}

	bool RenderPose( Pose? source, Pose? destination = null, float fraction = 0, LayerSnapshot? snapshot = null )
	{
		if ( !BlendLayerPose( source, destination, fraction, additive: false, snapshot, _composedPose, out var influence ) ||
			influence <= 0 || !ComposeLayers() ) return false;
		foreach ( var bone in _boneOrder )
		{
			var local = _composedPose[bone];
			var parent = _parents[bone];
			_modelPose[bone] = parent < 0 ? local : _modelPose[parent].ToWorld( local );
			_target.SetBoneOverride( bone, _modelPose[bone] );
		}
		_ownsOverrides = true;
		_target.Update( 0 );
		return true;
	}

	readonly record struct LayerSnapshot( Transform[] Bones, float Influence );

	bool BlendLayerPose( Pose? source, Pose? destination, float fraction, bool additive, LayerSnapshot? snapshot,
		Transform[] output, out float influence )
	{
		var sourceWeight = (snapshot?.Influence ?? source?.BoneWeight ?? 0) * (1 - fraction);
		var destinationWeight = (destination?.BoneWeight ?? 0) * fraction;
		influence = sourceWeight + destinationWeight;
		if ( influence <= 0 ) return true;

		if ( sourceWeight > 0 && snapshot is null && source is { } currentPose )
		{
			Array.Clear( _localPose );
			if ( !Accumulate( currentPose, _localPose, additive ) ) return false;
		}
		if ( destinationWeight > 0 && destination is { } nextPose )
		{
			Array.Clear( _nextLocalPose );
			if ( !Accumulate( nextPose, _nextLocalPose, additive ) ) return false;
		}

		var mix = destinationWeight / influence;
		foreach ( var bone in _boneOrder )
		{
			var value = sourceWeight > 0 ? snapshot?.Bones[bone] ?? _localPose[bone].Resolve() : _nextLocalPose[bone].Resolve();
			output[bone] = sourceWeight > 0 && destinationWeight > 0
				? global::Transform.Lerp( value, _nextLocalPose[bone].Resolve(), mix, clamp: true ) : value;
		}
		return true;
	}

	bool Accumulate( Pose pose, UnityPoseSum[] output, bool additive = false )
	{
		for ( var i = 0; i < pose.Weights.Length; i++ )
		{
			var leaf = pose.Tree.Nodes[i];
			if ( pose.Weights[i] <= 0 || leaf.Sequence is null ) continue;
			var weight = pose.Weights[i] / pose.BoneWeight;
			var sampler = GetSampler( leaf.Sequence );
			var sequence = sampler.CurrentSequence;
			var reference = additive ? GetAdditiveReference( leaf.Sequence, sampler ) : null;
			if ( sequence.Duration > 0 ) sequence.TimeNormalized = SamplePhase( pose, leaf );
			else if ( !sequence.SequenceNames.Contains( leaf.Sequence ) ) return false;
			sampler.Update( 0 );
			foreach ( var bone in _boneOrder )
			{
				var local = sampler.GetParentSpaceBone( bone );
				if ( reference is not null ) local = UnityAdditivePoseData.Delta( reference[bone], local );
				output[bone].Add( local, weight );
			}
		}
		return true;
	}

	SceneModel GetSampler( string sequence )
	{
		if ( _samplers.TryGetValue( sequence, out var existing ) ) return existing;
		// A sampler uses one sequence so native crossfades cannot affect the source pose.
		var sampler = new SceneModel( Scene.SceneWorld, _model, global::Transform.Zero ) { RenderingEnabled = false };
		sampler.CurrentSequence.Name = sequence;
		return _samplers[sequence] = sampler;
	}

	static float SamplePhase( Pose pose, UnityBlendTreeData.Node leaf )
	{
		var phase = UnwrappedPhase( pose, leaf );
		return leaf.SourceLoops ? phase - MathF.Floor( phase ) : Math.Clamp( phase, 0, 1 );
	}

	static float UnwrappedPhase( Pose pose, UnityBlendTreeData.Node leaf ) =>
		pose.Phase + pose.Tree.CycleOffset + leaf.CycleOffset + leaf.SourceCycleOffset;

	public void Stop()
	{
		if ( _ownsOverrides && _target.IsValid() )
		{
			_target.ClearBoneOverrides();
			_target.Update( 0 );
		}
		foreach ( var sampler in _samplers.Values )
			if ( sampler.IsValid() ) sampler.Delete();
		_samplers.Clear();
		_model = null;
		_target = null;
		_tree = null;
		ClearController();
		ClearLayers();
		_ownsOverrides = false;
	}

	protected override void OnDisabled() => Stop();
	protected override void OnDestroy() => Stop();
}

namespace UnityMount;

public sealed partial class UnityBlendTreePlayer
{
	readonly SortedDictionary<int, LayerPlayback> _layers = new();
	Dictionary<string, Transform[]> _additiveReferences;

	/// <summary>Plays an additional controller layer, following its source clock if synchronized.</summary>
	public bool PlayLayer( string layerName, string startState = null, float? weight = null ) =>
		StartLayer( layerName, startState, 0, weight, frozen: false );

	/// <summary>Samples another authored layer at an explicit phase over the playing controller layer.</summary>
	public bool SetLayerPose( string layerName, float phase, float? weight = null, string statePath = null ) =>
		StartLayer( layerName, statePath, phase, weight, frozen: true );

	bool StartLayer( string name, string statePath, float phase, float? weight, bool frozen )
	{
		if ( _controller is null || !float.IsFinite( phase ) || (weight is { } value && !float.IsFinite( value )) || !CanRender() )
			return false;
		var index = Array.FindIndex( _controller.Layers, layer => layer.Name == name );
		if ( index <= _controllerLayer ) return false;
		var layer = _controller.Layers[index];
		if ( (uint)layer.MachineIndex >= _controller.StateMachines.Length ||
			layer.Mode is not (UnityAnimatorControllerData.LayerMode.Override or UnityAnimatorControllerData.LayerMode.Additive) ) return false;

		var machine = _controller.StateMachines[layer.MachineIndex];
		var sourceIndex = -1;
		LayerPlayback clock = null;
		if ( !frozen && layer.SynchronizedLayerIndex > 0 )
		{
			if ( layer.SynchronizedLayerAffectsTiming ) return false;
			sourceIndex = Array.FindIndex( _controller.Layers, candidate =>
				candidate.MachineIndex == layer.MachineIndex && candidate.SynchronizedLayerIndex == 0 );
			if ( sourceIndex < 0 || FindLayer( sourceIndex ) is not { } active ) return false;
			if ( statePath is not null && statePath != active.StatePath ) return false;
			clock = active;
		}
		var stateIndex = clock?.Current.State ??
			(statePath is null ? _controller.FindEntryState( layer.MachineIndex, _parameters ) : FindState( machine, statePath ));
		if ( (uint)stateIndex >= machine.States.Length ) return false;

		var previous = _layers.GetValueOrDefault( index );
		var layerWeight = Math.Clamp( weight ?? layer.DefaultWeight, 0, 1 );
		var playback = previous is null ? CreatePlayback( layer, machine, stateIndex, phase, layerWeight, frozen )
			: new LayerPlayback( layer, machine, previous.Motions, new( stateIndex, phase ), layerWeight, frozen );
		playback.SynchronizedSource = sourceIndex;
		if ( clock is null && ControllerPose( playback, playback.Current ) is null && (frozen || !playback.IsEmpty( stateIndex )) ) return false;

		_layers[index] = playback;
		if ( RenderControllerPose() ) return true;
		if ( previous is null ) _layers.Remove( index );
		else _layers[index] = previous;
		return false;
	}

	/// <summary>Returns the current state of an active overlay layer.</summary>
	public string GetLayerStatePath( string layerName ) => _controller is { } graph
		&& _layers.GetValueOrDefault( Array.FindIndex( graph.Layers, layer => layer.Name == layerName ) ) is { } playback
		? ClockFor( playback )?.StatePath : null;

	LayerPlayback FindLayer( int index ) => index == _controllerLayer ? _body : _layers.GetValueOrDefault( index );

	LayerPlayback ClockFor( LayerPlayback layer ) => layer.SynchronizedSource < 0 ? layer : FindLayer( layer.SynchronizedSource );

	static LayerSnapshot? SnapshotFor( LayerPlayback layer, LayerSnapshot? source )
	{
		if ( layer.SynchronizedSource < 0 ) return source;
		return source is { } parent && layer.SynchronizedSnapshot is { } follower &&
			ReferenceEquals( follower.SourceBones, parent.Bones ) ? follower.Pose : null;
	}

	LayerPlayback[] FindSynchronizedLayers( int index ) =>
		_layers.Values.Where( follower => follower.SynchronizedSource == index ).ToArray();

	LayerSnapshot? CaptureInterruptedPose( LayerPlayback layer, LayerPlayback[] synchronized, StateTime source, Fade previous )
	{
		if ( CaptureLayer( layer, source, previous.Destination, previous.Progress, previous.Snapshot ) is not { } captured ) return null;
		var followerPoses = new LayerSnapshot[synchronized.Length];
		for ( var i = 0; i < synchronized.Length; i++ )
		{
			var follower = synchronized[i];
			if ( CaptureLayer( follower, source, previous.Destination, previous.Progress, SnapshotFor( follower, previous.Snapshot ) )
				is not { } pose ) return null;
			followerPoses[i] = pose;
		}
		for ( var i = 0; i < synchronized.Length; i++ )
			synchronized[i].SynchronizedSnapshot = (captured.Bones, followerPoses[i]);
		return captured;
	}

	LayerSnapshot? CaptureLayer( LayerPlayback layer, StateTime source, StateTime destination, float fraction, LayerSnapshot? previous )
	{
		// Unity freezes each participating layer's blended pose when interrupted.
		var bones = new Transform[_model.BoneCount];
		var additive = layer != _body && layer.Layer.Mode == UnityAnimatorControllerData.LayerMode.Additive;
		return BlendLayerPose( ControllerPose( layer, source ), ControllerPose( layer, destination ),
			fraction, additive, previous, bones, out var influence ) ? new( bones, influence ) : null;
	}

	public bool ClearLayerPose( string layerName )
	{
		if ( _controller is null || !CanRender() ) return false;
		var index = Array.FindIndex( _controller.Layers, layer => layer.Name == layerName );
		return _layers.Remove( index ) && RenderControllerPose();
	}

	bool ComposeLayers()
	{
		foreach ( var layer in _layers.Values )
		{
			if ( layer.Weight <= 0 || ClockFor( layer ) is not { } clock ) continue;
			var source = ControllerPose( layer, clock.Current );
			if ( source is null && layer.SynchronizedSource < 0 && !layer.IsEmpty( clock.Current.State ) ) return false;
			var destination = default(Pose?);
			var fraction = 0f;
			if ( clock.Fade is { Progress: > 0 } fade )
			{
				destination = ControllerPose( layer, fade.Destination );
				if ( destination is null && layer.SynchronizedSource < 0 && !layer.IsEmpty( fade.Destination.State ) ) return false;
				fraction = fade.Progress;
			}

			var additive = layer.Layer.Mode == UnityAnimatorControllerData.LayerMode.Additive;
			if ( !BlendLayerPose( source, destination, fraction, additive, SnapshotFor( layer, clock.Fade?.Snapshot ), _overlayPose, out var influence ) ) return false;
			if ( influence <= 0 ) continue;

			var mask = layer.Layer.BoneWeights;
			foreach ( var index in _boneOrder )
			{
				var weight = layer.Weight * influence;
				if ( mask is not null ) weight *= (uint)index < mask.Length ? Math.Clamp( mask[index], 0, 1 ) : 0;
				if ( weight <= 0 ) continue;
				var value = _overlayPose[index];
				var current = _composedPose[index];
				_composedPose[index] = additive
					? UnityAdditivePoseData.Apply( current, value, weight )
					: global::Transform.Lerp( current, value, weight, clamp: true );
			}
		}
		return true;
	}

	Transform[] GetAdditiveReference( string sequence, SceneModel sampler )
	{
		_additiveReferences ??= (_model.GetData<UnityAdditivePoseData[]>() ?? [])
			.Where( pose => pose.Bones.Length == _model.BoneCount )
			.ToDictionary( pose => pose.Sequence, pose => pose.Bones );
		if ( _additiveReferences.TryGetValue( sequence, out var reference ) ) return reference;
		// Unity uses the first pose when an explicit reference was not authored.
		sampler.CurrentSequence.TimeNormalized = 0;
		sampler.Update( 0 );
		return _additiveReferences[sequence] = Enumerable.Range( 0, _model.BoneCount ).Select( sampler.GetParentSpaceBone ).ToArray();
	}

	void ClearLayers()
	{
		_layers.Clear();
		_additiveReferences = null;
	}
}

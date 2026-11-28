namespace UnityMount;

public sealed partial class UnityBlendTreePlayer
{
	[Property] public bool AdvanceControllerOnUpdate { get; set; } = true;

	public string NextStatePath => _body?.Fade is { } fade ? _body.Machine.States[fade.Destination.State].Path : null;
	public float StatePhase => _body?.Current.Phase ?? 0;
	public float TransitionProgress => _body?.Fade?.Progress ?? 0;

	UnityAnimatorControllerData _controller;
	LayerPlayback _body;
	int _controllerLayer;
	UnityBlendTreeData[] _controllerTrees = [];
	Dictionary<string, UnityAnimatorParameterCurve[]> _parameterCurves = new();
	readonly Dictionary<string, float> _parameters = new();
	readonly Dictionary<string, float> _transitionParameters = new();
	readonly Dictionary<string, (float Value, float Weight)> _curveValues = new();

	readonly record struct StateTime( int State, float Phase, bool Entering = true );
	readonly record struct Fade( StateTime Destination, UnityAnimatorControllerData.Transition Rule, float Duration,
		float Progress = 0, LayerSnapshot? Snapshot = null );

	readonly record struct StateSample( StateTime Time, Pose? Pose, float Duration, float Rate );

	readonly record struct TransitionCandidate( UnityAnimatorControllerData.RuleMatch Match, float Delay, float Duration );

	sealed class LayerPlayback( UnityAnimatorControllerData.Layer layer, UnityAnimatorControllerData.Machine machine,
		UnityBlendTreeData[] motions, StateTime current, float weight, bool frozen )
	{
		public UnityAnimatorControllerData.Layer Layer { get; } = layer;
		public UnityAnimatorControllerData.Machine Machine { get; } = machine;
		public UnityBlendTreeData[] Motions { get; } = motions;
		public readonly float Weight = weight;
		public readonly bool Frozen = frozen;
		public StateTime Current { get; set; } = current;
		public Fade? Fade { get; set; }
		public int SynchronizedSource { get; set; } = -1;
		public (Transform[] SourceBones, LayerSnapshot Pose)? SynchronizedSnapshot { get; set; }
		public string StatePath => Machine.States[Current.State].Path;

		public bool IsEmpty( int state )
		{
			var indices = Machine.States[state].MotionIndices;
			var slot = Layer.SynchronizedLayerIndex;
			return (uint)slot < indices.Length && indices[slot] == -1;
		}
	}

	/// <summary>Plays one layer of the mounted controller, starting at its authored default unless a state is specified.</summary>
	public bool PlayController( string layerName, string startState = null, IReadOnlyDictionary<string, float> parameters = null )
	{
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() || Renderer.BoneMergeTarget.IsValid() ||
			Renderer.Model.GetData<UnityAnimatorControllerData>() is not { } graph ||
			graph.Layers.FirstOrDefault( layer => layer.Name == layerName ) is not { } layer ||
			(uint)layer.MachineIndex >= graph.StateMachines.Length ) return false;

		var machine = graph.StateMachines[layer.MachineIndex];
		var stateIndex = startState is null ? graph.FindEntryState( layer.MachineIndex, parameters ) : FindState( machine, startState );
		if ( (uint)stateIndex >= machine.States.Length || !PrepareRenderer() ) return false;

		_controller = graph;
		_controllerLayer = Array.IndexOf( graph.Layers, layer );
		_controllerTrees = _model.GetData<UnityBlendTreeData[]>() ?? [];
		_body = CreatePlayback( layer, machine, stateIndex, 0, 1, false );
		_parameterCurves = (_model.GetData<UnityAnimatorParameterCurve[]>() ?? []).GroupBy( curve => curve.Sequence )
			.ToDictionary( group => group.Key, group => group.ToArray() );

		foreach ( var parameter in graph.Parameters )
		{
			if ( parameter.Name is not { } name ) continue;
			if ( parameter.Type == UnityAnimatorControllerData.ParameterType.Trigger ) _triggerNames.Add( name );
			if ( parameter.Default is { } value ) SetControllerParameter( name, (float)value );
		}
		if ( parameters is not null )
			foreach ( var (name, value) in parameters ) SetControllerParameter( name, value );

		if ( RenderControllerPose() ) return true;
		Stop();
		return false;
	}

	public void SetControllerParameter( string name, float value )
	{
		if ( _controller is not null && !string.IsNullOrEmpty( name ) && float.IsFinite( value ) )
			_parameters[name] = _triggerNames.Contains( name ) ? (value != 0 ? 1 : 0) : value;
	}

	/// <summary>Evaluates and advances Animator transitions, then applies the current pose.</summary>
	public bool AdvanceController( float seconds )
	{
		if ( _controller is null || !float.IsFinite( seconds ) || seconds < 0 || !CanRender() ) return false;

		var body = _body;
		var handler = AnimationEvent;
		List<AnimationMarker> markers = handler is not null && LoadAnimationEvents() ? new() : null;
		_consumedTriggers.Clear();
		var advanced = AdvanceLayer( _controllerLayer, body, seconds, markers );
		if ( advanced )
			foreach ( var (index, layer) in _layers )
			{
				if ( layer.Frozen || layer.SynchronizedSource >= 0 || AdvanceLayer( index, layer, seconds, markers ) ) continue;
				advanced = false;
				break;
			}
		// A trigger can select transitions on several layers during the same update.
		foreach ( var name in _consumedTriggers ) _parameters[name] = 0;
		if ( !advanced || !RenderControllerPose() ) return false;

		if ( markers is { Count: > 0 } )
			foreach ( var marker in markers.OrderBy( marker => marker.SecondsIntoUpdate ) )
			{
				if ( !ReferenceEquals( _body, body ) ) break;
				handler( marker );
			}
		return true;
	}

	bool AdvanceLayer( int index, LayerPlayback layer, float seconds, List<AnimationMarker> markers )
	{
		_layerConsumedTriggers.Clear();
		var synchronized = FindSynchronizedLayers( index );
		var remaining = seconds;
		for ( var iteration = 0; iteration < 32; iteration++ )
		{
			var source = SampleState( layer, layer.Current, synchronized );
			if ( source.Pose is null && !layer.IsEmpty( source.Time.State ) ) return false;

			if ( layer.Fade is { } fade )
			{
				var destination = SampleState( layer, fade.Destination, synchronized );
				if ( destination.Pose is null && !layer.IsEmpty( destination.Time.State ) ) return false;
				var untilEnd = (1 - fade.Progress) * fade.Duration;
				var elapsed = MathF.Min( remaining, untilEnd );
				var interrupt = FindInterruption( layer, fade, source, destination, elapsed );
				if ( interrupt is { } candidate ) elapsed = candidate.Delay;

				var current = AdvanceState( source, elapsed );
				var next = AdvanceState( destination, elapsed );
				if ( markers is not null && elapsed > 0 )
				{
					var progress = Math.Clamp( fade.Progress + elapsed / fade.Duration, 0, 1 );
					if ( fade.Snapshot is null )
						CollectGroupEvents( markers, layer, synchronized, source, current, seconds - remaining, elapsed, 1 - fade.Progress, 1 - progress );
					CollectGroupEvents( markers, layer, synchronized, destination, next, seconds - remaining, elapsed, fade.Progress, progress );
				}

				layer.Current = current;
				remaining -= elapsed;
				if ( interrupt is { } change )
				{
					LayerSnapshot? snapshot = null;
					if ( change.Duration > 0 )
					{
						var progress = Math.Clamp( fade.Progress + elapsed / fade.Duration, 0, 1 );
						if ( CaptureInterruptedPose( layer, synchronized, current, fade with { Destination = next, Progress = progress } )
							is not { } captured ) return false;
						snapshot = captured;
					}
					BeginTransition( layer, change, snapshot );
				}
				else if ( elapsed < untilEnd )
				{
					layer.Fade = fade with { Destination = next, Progress = fade.Progress + elapsed / fade.Duration };
				}
				else
				{
					layer.Current = next;
					layer.Fade = null;
				}

				if ( remaining <= 0 ) break;
				continue;
			}

			var parameters = TransitionParameters( source.Pose );
			var time = source.Time;
			var end = time.Phase + remaining * source.Rate;
			var match = _controller.FindRule( layer.Machine, layer.Machine.AnyState, time.State, time.Phase, end, parameters );
			if ( match.Rule is null )
				match = _controller.FindRule( layer.Machine, layer.Machine.States[time.State].Transitions, time.State, time.Phase, end, parameters );
			if ( Candidate( layer, match, source, remaining ) is not { } transition )
			{
				layer.Current = AdvanceState( source, remaining );
				if ( markers is not null ) CollectGroupEvents( markers, layer, synchronized, source, layer.Current, seconds - remaining, remaining );
				break;
			}

			if ( transition.Match.Rule.HasExitTime )
			{
				layer.Current = time with { Phase = UnityAnimatorControllerData.NextExit( layer.Machine.States[time.State], transition.Match.Rule, time.Phase ),
					Entering = transition.Delay == 0 && time.Entering };
				if ( markers is not null ) CollectGroupEvents( markers, layer, synchronized, source, layer.Current, seconds - remaining, transition.Delay );
			}
			remaining -= transition.Delay;
			BeginTransition( layer, transition );
			if ( remaining <= 0 ) break;
		}

		return true;
	}

	TransitionCandidate? FindInterruption( LayerPlayback layer, Fade fade, StateSample source, StateSample destination, float elapsed )
	{
		var anyState = Search( layer.Machine.AnyState, source, fade.Progress > 0 );
		if ( anyState.Stop || anyState.Candidate is not null ) return anyState.Candidate;

		var mode = fade.Rule?.Interruption ?? UnityAnimatorControllerData.InterruptionSource.None;
		if ( mode is UnityAnimatorControllerData.InterruptionSource.Destination or UnityAnimatorControllerData.InterruptionSource.DestinationThenSource )
		{
			var result = Search( layer.Machine.States[destination.Time.State].Transitions, destination );
			if ( result.Stop || result.Candidate is not null || mode == UnityAnimatorControllerData.InterruptionSource.Destination )
				return result.Candidate;
		}
		if ( mode is UnityAnimatorControllerData.InterruptionSource.Source or UnityAnimatorControllerData.InterruptionSource.SourceThenDestination
			or UnityAnimatorControllerData.InterruptionSource.DestinationThenSource )
		{
			var result = Search( layer.Machine.States[source.Time.State].Transitions, source );
			if ( result.Stop || result.Candidate is not null ) return result.Candidate;
		}
		return mode == UnityAnimatorControllerData.InterruptionSource.SourceThenDestination
			? Search( layer.Machine.States[destination.Time.State].Transitions, destination ).Candidate : null;

		(bool Stop, TransitionCandidate? Candidate) Search( UnityAnimatorControllerData.Transition[] rules, StateSample sample, bool allowActive = false )
		{
			var time = sample.Time;
			var parameters = TransitionParameters( sample.Pose );
			var match = _controller.FindRule( layer.Machine, rules, time.State, time.Phase, time.Phase + elapsed * sample.Rate,
				parameters, fade.Rule, allowActive );
			return (match.Stop, Candidate( layer, match, sample, elapsed ));
		}
	}

	TransitionCandidate? Candidate( LayerPlayback layer, UnityAnimatorControllerData.RuleMatch match, StateSample sample, float remaining )
	{
		if ( match.Rule is not { } rule ) return null;
		if ( !CanPlayState( layer, new( match.Destination, rule.DestinationOffset ) ) ) return null;
		var time = sample.Time;
		var delay = rule.HasExitTime && sample.Rate > 0
			? Math.Clamp( (UnityAnimatorControllerData.NextExit( layer.Machine.States[time.State], rule, time.Phase ) - time.Phase) / sample.Rate, 0, remaining ) : 0;
		return new( match, delay, rule.FixedDuration ? rule.Duration : rule.Duration * sample.Duration );
	}

	bool CanPlayState( LayerPlayback layer, StateTime state )
	{
		var target = ControllerPose( layer, state );
		return layer == _body ? target is { BoneWeight: > 0 } : target is not null || layer.IsEmpty( state.State );
	}

	void BeginTransition( LayerPlayback layer, TransitionCandidate transition, LayerSnapshot? snapshot = null )
	{
		var rule = transition.Match.Rule;
		ConsumeTriggers( rule.Conditions );
		if ( transition.Match.SelectorConditions is { } conditions ) ConsumeTriggers( conditions );
		var destination = new StateTime( transition.Match.Destination, rule.DestinationOffset );
		if ( transition.Duration > 0 ) layer.Fade = new( destination, rule, transition.Duration, Snapshot: snapshot );
		else
		{
			layer.Fade = null;
			layer.Current = destination;
		}
	}

	bool RenderControllerPose()
	{
		if ( ControllerPose( _body, _body.Current ) is not { } pose ) return false;
		if ( _body.Fade is not { } active ) return RenderPose( pose );
		return ControllerPose( _body, active.Destination ) is { } nextPose && RenderPose( pose, nextPose, active.Progress, active.Snapshot );
	}

	protected override void OnUpdate()
	{
		if ( AdvanceControllerOnUpdate && _controller is not null ) AdvanceController( Time.Delta );
	}

	static StateTime AdvanceState( StateSample sample, float elapsed ) => elapsed <= 0 ? sample.Time
		: sample.Time with { Phase = sample.Time.Phase + elapsed * sample.Rate, Entering = false };

	StateSample SampleState( LayerPlayback layer, StateTime time, LayerPlayback[] synchronized )
	{
		var pose = ControllerPose( layer, time );
		var duration = pose is { } motion ? MotionDuration( motion ) : 0;
		// A synchronized state can put its only motion on a follower.
		foreach ( var follower in synchronized )
		{
			if ( duration > 0 ) break;
			if ( ControllerPose( follower, time ) is { } alternate ) duration = MotionDuration( alternate );
		}
		return new( time, pose, duration, PhaseRate( layer.Machine.States[time.State], duration ) );
	}

	Pose? ControllerPose( LayerPlayback layer, StateTime time ) =>
		EvaluateState( layer.Machine.States[time.State], layer.Motions[time.State], time.Phase );

	static int FindState( UnityAnimatorControllerData.Machine machine, string path ) =>
		Array.FindIndex( machine.States, state => state.Path == path );

	LayerPlayback CreatePlayback( UnityAnimatorControllerData.Layer layer, UnityAnimatorControllerData.Machine machine,
		int state, float phase, float weight, bool frozen ) =>
		new( layer, machine, machine.States.Select( state => ResolveMotion( state, layer.SynchronizedLayerIndex, _controllerTrees ) ).ToArray(),
			new( state, phase ), weight, frozen );

	Pose? EvaluateState( UnityAnimatorControllerData.State state, UnityBlendTreeData tree, float normalizedPhase )
	{
		if ( tree is null || state.Mirrored || Parameter( state.MirrorParameter, 0 ) != 0 ||
			UnityBlendWeights.Evaluate( tree, _parameters ) is not { } weights ) return null;
		var phase = Parameter( state.TimeParameter, normalizedPhase ) + Parameter( state.CycleOffsetParameter, 0 );
		return EvaluatePose( tree, weights, phase );
	}

	float MotionDuration( Pose pose )
	{
		var duration = 0f;
		for ( var i = 0; i < pose.Weights.Length; i++ )
		{
			var weight = pose.Weights[i];
			if ( weight <= 0 ) continue;
			var node = pose.Tree.Nodes[i];
			duration += (node.UnboundClip?.Duration ?? GetSampler( node.Sequence ).CurrentSequence.Duration) * weight;
		}
		return duration;
	}

	float PhaseRate( UnityAnimatorControllerData.State state, float duration ) =>
		duration > 0 ? state.Speed * Parameter( state.SpeedParameter, 1 ) / duration : 0;

	float Parameter( string name, float fallback ) => name is not null && _parameters.TryGetValue( name, out var value ) ? value : fallback;

	IReadOnlyDictionary<string, float> TransitionParameters( Pose? sample )
	{
		_curveValues.Clear();
		if ( sample is { } pose )
		{
			for ( var i = 0; i < pose.Weights.Length; i++ )
			{
				var weight = pose.Weights[i];
				var node = pose.Tree.Nodes[i];
				var clipName = node.Sequence ?? node.UnboundClip?.Name;
				if ( weight <= 0 || clipName is null || !_parameterCurves.TryGetValue( clipName, out var curves ) ) continue;
				var phase = SamplePhase( pose, node );
				foreach ( var curve in curves )
				{
					if ( _triggerNames.Contains( curve.Parameter ) ) continue;
					var (total, combined) = _curveValues.GetValueOrDefault( curve.Parameter );
					_curveValues[curve.Parameter] = (total + curve.Sample( phase * curve.Duration ) * weight, combined + weight);
				}
			}
		}
		if ( _curveValues.Count == 0 && _layerConsumedTriggers.Count == 0 ) return _parameters;

		_transitionParameters.Clear();
		foreach ( var (name, value) in _parameters ) _transitionParameters[name] = value;
		foreach ( var (name, (value, weight)) in _curveValues ) _transitionParameters[name] = value / weight;
		foreach ( var name in _layerConsumedTriggers ) _transitionParameters[name] = 0;
		return _transitionParameters;
	}

	static UnityBlendTreeData ResolveMotion( UnityAnimatorControllerData.State state, int synchronizedIndex, UnityBlendTreeData[] trees )
	{
		if ( (uint)synchronizedIndex >= state.MotionIndices.Length ) return null;
		var index = state.MotionIndices[synchronizedIndex];
		var motion = state.Motions.FirstOrDefault( item => item.TreeIndex == index );
		if ( motion is null ) return null;
		if ( motion.Sequence is null && motion.UnboundClip is null )
			return trees.FirstOrDefault( tree => tree.StatePath == state.Path && tree.TreeIndex == index );

		return new()
		{
			StatePath = state.Path,
			CycleOffset = state.CycleOffset,
			Nodes = [new()
			{
				Sequence = motion.Sequence,
				UnboundClip = motion.UnboundClip,
				CycleOffset = motion.CycleOffset,
				Mirrored = motion.Mirrored,
				SourceCycleOffset = motion.SourceCycleOffset,
				SourceLoops = motion.SourceLoops
			}]
		};
	}

	void ClearController()
	{
		_controller = null;
		_body = null;
		_controllerTrees = [];
		_parameterCurves.Clear();
		_animationEvents = null;
		_parameters.Clear();
		ClearTriggers();
		_transitionParameters.Clear();
		_curveValues.Clear();
	}
}

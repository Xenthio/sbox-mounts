using System.Text.Json.Serialization;

namespace UnityMount;

/// <summary>Authored Animator states, parameter defaults and ordered transition rules for a mounted model.</summary>
[Sandbox.ModelEditor.GameData( "unity_animator_controller" ), Hide]
public sealed record UnityAnimatorControllerData
{
	const JsonNumberHandling ExactNumbers = JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString;

	public enum ParameterType { Float = 1, Int = 3, Bool = 4, Trigger = 9 }
	public enum LayerMode { Override, Additive }
	public enum ConditionMode { If = 1, IfNot = 2, Greater = 3, Less = 4, Equals = 6, NotEqual = 7 }
	public enum InterruptionSource { None, Source, Destination, SourceThenDestination, DestinationThenSource }

	public Parameter[] Parameters { get; init; } = [];
	public Layer[] Layers { get; init; } = [];
	public Machine[] StateMachines { get; init; } = [];

	[JsonNumberHandling( ExactNumbers )]
	public sealed record Parameter( string Name, ParameterType Type, double? Default );
	public sealed record MaskBone( uint PathHash, float Weight );
	public sealed record Layer( string Name, int MachineIndex, int SynchronizedLayerIndex, LayerMode Mode = LayerMode.Override,
		float DefaultWeight = 1, MaskBone[] SkeletonMask = null, float[] BoneWeights = null, bool SynchronizedLayerAffectsTiming = false );
	public sealed record Machine( int DefaultStateIndex, State[] States, Transition[] AnyState, Selector[] Selectors = null );
	public sealed record Selector( uint PathHash, bool IsEntry, SelectorTransition[] Transitions );
	public sealed record SelectorTransition( int DestinationIndex, Condition[] Conditions );
	[JsonNumberHandling( ExactNumbers )]
	public sealed record State( string Name, string Path, bool Looping, float Speed, string SpeedParameter, float CycleOffset,
		string CycleOffsetParameter, string TimeParameter, bool Mirrored, string MirrorParameter,
		int[] MotionIndices, Motion[] Motions, Transition[] Transitions );
	public sealed record Motion( int TreeIndex, int ClipSlot, string Sequence, float CycleOffset = 0, bool Mirrored = false,
		float SourceCycleOffset = 0, bool SourceLoops = false, UnityBlendTreeData.UnboundMotion UnboundClip = null );
	[JsonNumberHandling( ExactNumbers )]
	public sealed record Condition( string Parameter, ConditionMode Mode, float Threshold, float ExitTime );
	[JsonNumberHandling( ExactNumbers )]
	public sealed record Transition( int DestinationIndex, string Path, bool HasExitTime, float ExitTime, bool FixedDuration, float Duration,
		float DestinationOffset, InterruptionSource Interruption, bool OrderedInterruption, bool CanTransitionToSelf, Condition[] Conditions );

	/// <summary>Finds the first eligible transition with a reachable destination between two unwrapped normalized phases.</summary>
	public Transition FindTransition( int machineIndex, int stateIndex, float previousPhase, float phase,
		IReadOnlyDictionary<string, float> parameters )
	{
		if ( (uint)machineIndex >= StateMachines.Length || phase < previousPhase ) return null;
		var machine = StateMachines[machineIndex];
		if ( (uint)stateIndex >= machine.States.Length ) return null;

		var result = FindRule( machine, machine.AnyState, stateIndex, previousPhase, phase, parameters );
		if ( result.Rule is null )
			result = FindRule( machine, machine.States[stateIndex].Transitions, stateIndex, previousPhase, phase, parameters );
		return result.Rule is not { } rule || rule.DestinationIndex == result.Destination
			? result.Rule : rule with { DestinationIndex = result.Destination };
	}

	internal readonly record struct RuleMatch( Transition Rule, int Destination, bool Stop = false, List<Condition> SelectorConditions = null );

	internal RuleMatch FindRule( Machine machine, Transition[] transitions, int stateIndex, float previousPhase, float phase,
		IReadOnlyDictionary<string, float> parameters, Transition active = null, bool allowActive = false )
	{
		if ( !float.IsFinite( previousPhase ) || !float.IsFinite( phase ) ) return default;
		var state = machine.States[stateIndex];
		foreach ( var transition in transitions )
		{
			var isActive = ReferenceEquals( active, transition );
			if ( isActive && active.OrderedInterruption ) return new( null, -1, true );
			if ( isActive && !allowActive ) continue;
			if ( transition.HasExitTime )
			{
				var exit = NextExit( state, transition, previousPhase );
				if ( exit <= previousPhase || exit > phase ) continue;
			}

			if ( !transition.Conditions.All( condition => Matches( condition, parameters ) ) ) continue;
			var destination = ResolveDestination( machine, transition.DestinationIndex, parameters, out var selectorConditions );
			if ( destination < 0 || (!transition.CanTransitionToSelf && destination == stateIndex) ) continue;
			return new( transition, destination, SelectorConditions: selectorConditions );
		}
		return default;
	}

	internal static float NextExit( State state, Transition transition, float phase )
	{
		var exit = transition.ExitTime;
		return state.Looping && exit < 1 ? exit + (MathF.Floor( phase - exit ) + 1) : exit;
	}

	const int FirstSelectorIndex = 30000;

	internal int FindEntryState( int machineIndex, IReadOnlyDictionary<string, float> parameters )
	{
		var machine = StateMachines[machineIndex];
		if ( machine.Selectors is not { Length: > 0 } || !machine.Selectors[0].IsEntry ) return machine.DefaultStateIndex;
		var entry = ResolveDestination( machine, FirstSelectorIndex, parameters, out _ );
		return entry < 0 ? machine.DefaultStateIndex : entry;
	}

	int ResolveDestination( Machine machine, int destination, IReadOnlyDictionary<string, float> parameters,
		out List<Condition> selectorConditions )
	{
		selectorConditions = null;
		var selectors = machine.Selectors ?? [];
		// Selector indices are stored after the state range. Limit traversal to reject cycles.
		for ( var visited = 0; visited <= selectors.Length; visited++ )
		{
			if ( (uint)destination < machine.States.Length ) return destination;
			var index = destination - FirstSelectorIndex;
			if ( (uint)index >= selectors.Length ) return -1;
			var route = selectors[index].Transitions.FirstOrDefault( entry => entry.Conditions.All( condition => Matches( condition, parameters ) ) );
			if ( route is null ) return -1;
			if ( route.Conditions.Length > 0 ) (selectorConditions ??= new()).AddRange( route.Conditions );
			destination = route.DestinationIndex;
		}
		return -1;
	}

	bool Matches( Condition condition, IReadOnlyDictionary<string, float> parameters )
	{
		if ( condition.Parameter is null ) return false;
		var value = parameters is not null && parameters.TryGetValue( condition.Parameter, out var supplied )
			? supplied : Parameters.FirstOrDefault( parameter => parameter.Name == condition.Parameter )?.Default;
		if ( value is not { } input || !double.IsFinite( input ) ) return false;
		return condition.Mode switch
		{
			ConditionMode.If => input != 0,
			ConditionMode.IfNot => input == 0,
			ConditionMode.Greater => input > condition.Threshold,
			ConditionMode.Less => input < condition.Threshold,
			ConditionMode.Equals => input == condition.Threshold,
			ConditionMode.NotEqual => input != condition.Threshold,
			_ => false
		};
	}

	internal UnityAnimatorControllerData Bind( IReadOnlyList<UnityBlendTreeData.SequenceInfo?> sequences,
		UnitySkin.Joint[] joints, byte[] indices ) =>
		this with
		{
			Layers = Layers.Select( layer => layer.SkeletonMask is { Length: > 0 }
				? layer with { BoneWeights = BindMask( layer.SkeletonMask, joints, indices ) } : layer ).ToArray(),
			StateMachines = StateMachines.Select( machine => machine with
			{
				States = machine.States.Select( state => state with
				{
					Motions = state.Motions.Select( motion => (uint)motion.ClipSlot < sequences.Count && sequences[motion.ClipSlot] is { } sequence
						? motion with { Sequence = sequence.Name, UnboundClip = sequence.UnboundClip,
							SourceCycleOffset = sequence.CycleOffset, SourceLoops = sequence.Looping }
						: motion ).ToArray()
				} ).ToArray()
			} ).ToArray()
		};

	static float[] BindMask( MaskBone[] source, UnitySkin.Joint[] joints, byte[] indices )
	{
		var mask = new Dictionary<uint, float>();
		foreach ( var entry in source ) mask[entry.PathHash] = entry.Weight;
		var weights = new float[joints.Length];
		for ( var i = 0; i < joints.Length; i++ )
			foreach ( var hash in joints[i].PathHashes )
			{
				if ( !mask.TryGetValue( hash, out var weight ) ) continue;
				weights[indices[i]] = weight;
				break;
			}
		return weights;
	}
}

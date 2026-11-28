namespace UnityMount;

public sealed partial class UnityBlendTreePlayer
{
	/// <summary>A clip marker reached during a controller update, with its layer and sampled weight.</summary>
	public readonly record struct AnimationMarker( string Layer, string StatePath, UnityAnimationEventData Event,
		float SecondsIntoUpdate, float Weight );

	/// <summary>Raised after the controller applies its pose. Explicit seeks do not emit markers.</summary>
	public event Action<AnimationMarker> AnimationEvent;

	ILookup<string, UnityAnimationEventData> _animationEvents;

	bool LoadAnimationEvents()
	{
		_animationEvents ??= (_model.GetData<UnityAnimationEventData[]>() ?? [])
			.Where( marker => !string.IsNullOrEmpty( marker.Sequence ) ).ToLookup( marker => marker.Sequence );
		return _animationEvents.Count > 0;
	}

	void CollectGroupEvents( List<AnimationMarker> pending, LayerPlayback source, LayerPlayback[] followers, StateSample state,
		StateTime next, float offset, float elapsed, float startWeight = 1, float endWeight = 1 )
	{
		CollectAnimationEvents( pending, source, state, next, offset, elapsed, startWeight, endWeight );
		foreach ( var follower in followers )
		{
			var sample = state with { Pose = ControllerPose( follower, state.Time ) };
			CollectAnimationEvents( pending, follower, sample, next, offset, elapsed, startWeight, endWeight );
		}
	}

	void CollectAnimationEvents( List<AnimationMarker> pending, LayerPlayback layer, StateSample state, StateTime next, float updateOffset,
		float elapsed, float startWeight = 1, float endWeight = 1 )
	{
		if ( elapsed <= 0 || layer.Weight <= 0 || MathF.Max( startWeight, endWeight ) <= 0 ||
			state.Pose is not { } pose || state.Rate == 0 ) return;
		var authored = layer.Machine.States[state.Time.State];
		if ( !string.IsNullOrEmpty( authored.TimeParameter ) && _parameters.ContainsKey( authored.TimeParameter ) ) return;

		var endPose = pose with { Phase = next.Phase + Parameter( authored.CycleOffsetParameter, 0 ) };
		for ( var i = 0; i < pose.Weights.Length; i++ )
		{
			var weight = pose.Weights[i];
			if ( weight <= 0 ) continue;
			var leaf = pose.Tree.Nodes[i];
			var name = leaf.Sequence ?? leaf.UnboundClip?.Name;
			if ( name is null || !_animationEvents.Contains( name ) ) continue;

			double start = UnwrappedPhase( pose, leaf );
			double end = UnwrappedPhase( endPose, leaf );
			var forward = end > start;
			if ( !double.IsFinite( end ) || start == end ) continue;
			foreach ( var marker in _animationEvents[name] )
			{
				var phase = marker.ClipPhase ?? LegacyPhase( marker, leaf );
				if ( phase is not (>= 0 and <= 1) ) continue;
				if ( !leaf.SourceLoops )
				{
					if ( Crossed( phase ) ) Record( phase, marker );
					continue;
				}

				var cycle = forward ? Math.Floor( start - phase ) + 1 : Math.Ceiling( start - phase ) - 1;
				var first = start - phase;
				if ( state.Time.Entering && first == Math.Truncate( first ) &&
					(forward ? phase < 1 : phase > 0) ) cycle = first;
				// Bound the work if an update skips an exceptionally large number of loops.
				for ( var repeat = 0; repeat < 1024; repeat++, cycle += forward ? 1 : -1 )
				{
					var occurrence = cycle + phase;
					if ( !Crossed( occurrence ) ) break;
					Record( occurrence, marker );
				}
			}

			bool Crossed( double phase ) => forward
				? phase <= end && (phase > start || (state.Time.Entering && phase == start))
				: phase >= end && (phase < start || (state.Time.Entering && phase == start));

			void Record( double phase, UnityAnimationEventData marker )
			{
				var fraction = (float)((phase - start) / (end - start));
				var blend = startWeight + (endWeight - startWeight) * fraction;
				pending.Add( new( layer.Layer.Name, authored.Path, marker, updateOffset + elapsed * fraction,
					layer.Weight * weight * blend ) );
			}
		}
	}

	float LegacyPhase( UnityAnimationEventData marker, UnityBlendTreeData.Node leaf )
	{
		var duration = leaf.UnboundClip?.Duration ?? GetSampler( leaf.Sequence ).CurrentSequence.Duration;
		return duration > 0 ? marker.Time / duration : float.NaN;
	}
}

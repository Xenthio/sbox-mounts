namespace UnityMount;

public sealed partial class UnityBlendTreePlayer
{
	readonly HashSet<string> _triggerNames = new();
	readonly HashSet<string> _consumedTriggers = new();
	readonly HashSet<string> _layerConsumedTriggers = new();

	/// <summary>Arms an authored trigger until a transition uses it.</summary>
	public bool SetControllerTrigger( string name )
	{
		if ( !_triggerNames.Contains( name ) ) return false;
		_parameters[name] = 1;
		return true;
	}

	/// <summary>Cancels a pending trigger.</summary>
	public bool ResetControllerTrigger( string name )
	{
		if ( !_triggerNames.Contains( name ) ) return false;
		_parameters[name] = 0;
		return true;
	}

	void ConsumeTriggers( IEnumerable<UnityAnimatorControllerData.Condition> conditions )
	{
		foreach ( var condition in conditions )
		{
			if ( condition.Mode != UnityAnimatorControllerData.ConditionMode.If || !_triggerNames.Contains( condition.Parameter ) ) continue;
			_layerConsumedTriggers.Add( condition.Parameter );
			_consumedTriggers.Add( condition.Parameter );
		}
	}

	void ClearTriggers()
	{
		_triggerNames.Clear();
		_consumedTriggers.Clear();
		_layerConsumedTriggers.Clear();
	}
}

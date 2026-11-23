using System;
using GoldSrc.HalfLife;
using GoldSrc.Maps;

namespace GoldSrc.CryOfFear;

[Hide]
public sealed class TimedSpawn : MapEntity
{
	const int MarkerLimit = 16;
	const float DeathCheckInterval = 0.25f;

	[Property] public string MarkerName { get; set; }
	[Property] public string ChildClass { get; set; }
	[Property] public int MinDelay { get; set; }
	[Property] public int MaxDelay { get; set; }
	[Property] public int ReplacementLimit { get; set; }

	internal enum Phase { AwaitingPlayer, Waiting, WatchingChild, Finished }
	internal sealed record SavedChild( Transform Transform, float Health, int Body, int Skin, Npc.SavedMagazine Magazine = null );
	internal sealed record SavedSpawner( Phase State, float RemainingTime, int Replacements, SavedChild Child );

	Phase _state;
	TimeUntil _nextStep;
	int _replacements;
	Npc _child;

	internal SavedSpawner Capture() => new( _state, _nextStep, _replacements, _child.IsValid() && _child.Health > 0
		? new SavedChild( _child.WorldTransform, _child.Health, _child.Body, _child.Skin, _child.CaptureMagazine() )
		: null );

	internal void Restore( SavedSpawner save )
	{
		if ( _child.IsValid() ) _child.GameObject.Destroy();
		_child = save.Child is { } child ? Spawn( child.Transform, child ) : null;
		_state = save.State;
		_nextStep = save.RemainingTime;
		_replacements = save.Replacements;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost || !Map.IsValid() || _nextStep > 0 ) return;
		switch ( _state )
		{
			case Phase.AwaitingPlayer:
				if ( Scene.GetAllComponents<PlayerController>().Any() ) ScheduleSpawn( MinDelay );
				break;
			case Phase.Waiting:
				var marker = SelectMarker();
				_child = marker is not null ? Spawn( marker.WorldTransform ) : null;
				_state = _child.IsValid() ? Phase.WatchingChild : Phase.Finished;
				break;
			case Phase.WatchingChild:
				CheckChild();
				break;
		}
	}

	void ScheduleSpawn( float delay )
	{
		_nextStep = delay + Game.Random.Float( 0, Math.Max( 0, MaxDelay - MinDelay ) );
		_state = Phase.Waiting;
	}

	void CheckChild()
	{
		_nextStep = DeathCheckInterval;
		if ( _child.IsValid() && _child.Health > 0 ) return;
		_child = null;
		if ( ReplacementLimit > 0 && ++_replacements > ReplacementLimit )
		{
			_state = Phase.Finished;
			return;
		}

		// With a positive minimum, the limited respawn branch overwrites it with one death poll.
		var delay = ReplacementLimit > 0 && MinDelay > 0 ? DeathCheckInterval : MinDelay;
		ScheduleSpawn( delay );
	}

	MapEntity SelectMarker()
	{
		var markers = Map.FindAll( MarkerName ).OrderBy( marker => marker.EntityIndex ).Take( MarkerLimit ).ToArray();
		if ( markers.Length == 0 ) return null;

		// GoldSrc rolls an inclusive upper bound and folds it back onto the last marker.
		var index = Math.Min( Game.Random.Int( 0, markers.Length ), markers.Length - 1 );
		return markers[index];
	}

	Npc Spawn( Transform transform, SavedChild saved = null )
	{
		if ( string.IsNullOrEmpty( ChildClass ) ) return null;
		var prefab = GameObject.GetPrefab( $"mount://cof/entities/npcs/{ChildClass}.prefab" );
		if ( !prefab.IsValid() ) return null;

		var actor = Monster.CloneSpawn( prefab, Map, transform, ChildClass );
		var npc = actor.Npc;
		if ( !npc.IsValid() )
		{
			actor.GameObject.Destroy();
			return null;
		}

		if ( saved is not null )
		{
			npc.Health = saved.Health;
			npc.Body = saved.Body;
			npc.Skin = saved.Skin;
			npc.RestoreMagazine( saved.Magazine );
		}
		actor.GameObject.Enabled = true;
		actor.GameObject.NetworkSpawn( true, null );
		return npc;
	}
}

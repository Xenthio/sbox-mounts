using GoldSrc.Maps;
using Sandbox.Npcs.Layers;

namespace GoldSrc.SvenCoop;

public sealed partial class Stukabat
{
	GameObject _lastSeenEnemy;
	Vector3 _lastSeenPosition;
	TimeUntil _searchUntil;
	Vector3[] _airRoute = [];
	int _airWaypoint;
	TimeUntil _replanAt;

	BBox FlightHull => new( new Vector3( -Definition.Radius, -Definition.Radius, 0 ),
		new Vector3( Definition.Radius, Definition.Radius, Definition.Height ) );

	void RememberEnemy( GameObject target, Vector3 flightPosition )
	{
		if ( _lastSeenEnemy != target ) ClearAirRoute();
		_lastSeenEnemy = target;
		_lastSeenPosition = flightPosition;
		_searchUntil = 5;
	}

	bool SearchForEnemy()
	{
		if ( _lastSeenEnemy is null ) return false;
		if ( _searchUntil <= 0 || !IsLiveTarget( _lastSeenEnemy ) || WorldPosition.DistanceSquared( _lastSeenPosition ) <= 20 * 20 )
		{
			_lastSeenEnemy = null;
			ClearAirRoute();
			Movement?.Stop();
			return false;
		}
		FlyAfterEnemy( null );
		return true;
	}

	void ClearAirRoute()
	{
		_airRoute = [];
		_airWaypoint = 0;
		_replanAt = 0;
	}

	void FlyAfterEnemy( GameObject target )
	{
		if ( Movement is not FlyingLayer flight ) return;
		var waypoint = _lastSeenPosition;
		var direct = MapState.AirLinkClear( GameObject, FlightHull, WorldPosition, waypoint, target );
		var stop = 19f;

		if ( direct ) ClearAirRoute();
		else if ( GetComponent<Monster>()?.Map is { } map && map.IsValid() )
		{
			var failed = flight.Status == MovementLayer.MovementStatus.Failed;
			var changedGoal = _airRoute.Length == 0 || _airRoute[^1].DistanceSquared( _lastSeenPosition ) > 64 * 64;
			if ( _replanAt <= 0 && (failed || changedGoal) )
			{
				_airRoute = map.FindAirRoute( GameObject, FlightHull, _lastSeenPosition, target );
				_airWaypoint = 0;
				_replanAt = 0.5f;
				if ( failed ) flight.Stop();
			}

			if ( _airWaypoint < _airRoute.Length - 1 )
			{
				if ( WorldPosition.DistanceSquared( _airRoute[_airWaypoint] ) <= 12 * 12 ) _airWaypoint++;
				waypoint = _airRoute[_airWaypoint];
				stop = _airWaypoint == _airRoute.Length - 1 ? 19 : 8;
			}
		}

		Diving = waypoint.z + Definition.Height * 0.5f - AttackOrigin.z < -64;
		flight.MoveTo( waypoint, stop, Diving ? Definition.Speed : 180,
			waypoint == _lastSeenPosition ? target : null );
	}
}

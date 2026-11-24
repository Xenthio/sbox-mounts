namespace GoldSrc.Maps;

public sealed partial class MapState
{
	[Property, Hide] public List<Vector3> AirNodes { get; set; } = [];

	internal static bool AirLinkClear( GameObject flyer, BBox hull, Vector3 from, Vector3 to, GameObject target )
	{
		var trace = flyer.Scene.Trace.Box( hull, from, to )
			.IgnoreGameObjectHierarchy( flyer ).WithoutTags( "npc", "player", "trigger", "debris" );
		if ( target.IsValid() ) trace = trace.IgnoreGameObjectHierarchy( target );
		var result = trace.Run();
		return !result.Hit && !result.StartedSolid;
	}

	internal Vector3[] FindAirRoute( GameObject flyer, BBox hull, Vector3 destination, GameObject target )
	{
		if ( AirNodes.Count == 0 ) return [];

		var transform = WorldTransform;
		var positions = AirNodes.Select( node => transform.PointToWorld( node ) ).ToList();
		var start = positions.Count;
		positions.Add( flyer.WorldPosition );
		var distance = Enumerable.Repeat( float.PositiveInfinity, positions.Count ).ToArray();
		var previous = new int[positions.Count];
		var closed = new bool[positions.Count];
		var open = new System.Collections.Generic.PriorityQueue<int, float>();
		distance[start] = 0;
		open.Enqueue( start, positions[start].Distance( destination ) );

		while ( open.TryDequeue( out var current, out _ ) )
		{
			if ( closed[current] ) continue;
			closed[current] = true;
			var point = positions[current];
			if ( current != start && AirLinkClear( flyer, hull, point, destination, target ) )
			{
				var route = new List<Vector3> { destination };
				for ( var index = current; index != start; index = previous[index] )
					route.Add( positions[index] );
				route.Reverse();
				return route.ToArray();
			}

			// Air nodes can link to any visible node, with the NPC's hull determining clearance.
			for ( var next = 0; next < start; next++ )
			{
				if ( closed[next] ) continue;
				var cost = distance[current] + point.Distance( positions[next] );
				if ( cost >= distance[next] || !AirLinkClear( flyer, hull, point, positions[next], target ) ) continue;
				distance[next] = cost;
				previous[next] = current;
				open.Enqueue( next, cost + positions[next].Distance( destination ) );
			}
		}

		return [];
	}
}

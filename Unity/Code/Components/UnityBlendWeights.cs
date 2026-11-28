namespace UnityMount;

static class UnityBlendWeights
{
	public static float[] Evaluate( UnityBlendTreeData tree, IReadOnlyDictionary<string, float> parameters )
	{
		if ( tree.Nodes.Length == 0 || tree.Mirrored ) return null;
		var leaves = new float[tree.Nodes.Length];
		var remaining = Math.Max( 1, tree.Nodes.Length * 4 );
		return Expand( 0, 1, 0 ) ? leaves : null;

		bool Expand( int index, float weight, int depth )
		{
			if ( depth > 64 || remaining-- <= 0 || (uint)index >= tree.Nodes.Length ) return false;
			var node = tree.Nodes[index];
			if ( node.Mirrored ) return false;
			if ( node.Children.Length == 0 )
			{
				leaves[index] += weight;
				return !string.IsNullOrEmpty( node.Sequence ) || node.UnboundClip is not null;
			}

			var x = Parameter( node.ParameterX );
			var y = Parameter( node.ParameterY );
			var point = new Vector2( x, y );
			var weights = node.Type switch
			{
				UnityBlendTreeData.BlendType.Simple1D => Linear( node, x ),
				UnityBlendTreeData.BlendType.SimpleDirectional2D => SimpleDirectional( node, point ),
				UnityBlendTreeData.BlendType.FreeformCartesian2D => Freeform( node, point, false ),
				UnityBlendTreeData.BlendType.FreeformDirectional2D => Freeform( node, point, true ),
				_ => null
			};
			if ( weights is null ) return false;
			for ( var i = 0; i < weights.Length; i++ )
				if ( weights[i] > 0 && !Expand( node.Children[i], weight * weights[i], depth + 1 ) ) return false;
			return true;
		}

		float Parameter( string name ) => name is not null && parameters.TryGetValue( name, out var value ) && float.IsFinite( value ) ? value : 0;
	}

	static float[] Linear( UnityBlendTreeData.Node node, float input )
	{
		var thresholds = node.Thresholds;
		if ( thresholds.Length != node.Children.Length ) return null;
		var weights = new float[thresholds.Length];
		if ( input <= thresholds[0] )
		{
			weights[0] = 1;
			return weights;
		}
		for ( var i = 1; i < thresholds.Length; i++ )
		{
			if ( input > thresholds[i] || thresholds[i] <= thresholds[i - 1] ) continue;
			var fraction = (input - thresholds[i - 1]) / (thresholds[i] - thresholds[i - 1]);
			weights[i - 1] = 1 - fraction;
			weights[i] = fraction;
			return weights;
		}
		weights[^1] = 1;
		return weights;
	}

	static float[] SimpleDirectional( UnityBlendTreeData.Node node, Vector2 input )
	{
		var points = node.Positions;
		if ( points.Length != node.Children.Length ) return null;
		var weights = new float[points.Length];
		var center = Array.IndexOf( points, Vector2.Zero );
		var bestLeft = float.PositiveInfinity;
		var bestRight = float.PositiveInfinity;
		var left = -1;
		var right = -1;
		if ( input != Vector2.Zero )
		{
			for ( var i = 0; i < points.Length; i++ )
			{
				if ( points[i] == Vector2.Zero ) continue;
				var angle = AngleBetween( input, points[i] );
				if ( angle >= 0 && angle < bestLeft ) (left, bestLeft) = (i, angle);
				if ( angle <= 0 && -angle < bestRight ) (right, bestRight) = (i, -angle);
			}
		}

		if ( left >= 0 && left == right )
			weights[left] = Math.Clamp( input.Length / points[left].Length, 0, 1 );
		else if ( left >= 0 && right >= 0 && bestLeft + bestRight < MathF.PI )
		{
			var denominator = Cross( points[right], points[left] );
			if ( denominator > 0.000001f )
			{
				var leftWeight = MathF.Max( 0, Cross( points[right], input ) / denominator );
				var rightWeight = MathF.Max( 0, Cross( input, points[left] ) / denominator );
				var normalization = MathF.Max( 1, leftWeight + rightWeight );
				weights[left] = leftWeight / normalization;
				weights[right] = rightWeight / normalization;
			}
		}

		var remaining = MathF.Max( 0, 1 - weights.Sum() );
		if ( center >= 0 ) weights[center] = remaining;
		else
			for ( var i = 0; i < weights.Length; i++ ) weights[i] += remaining / weights.Length;
		return weights;
	}

	static float[] Freeform( UnityBlendTreeData.Node node, Vector2 input, bool directional )
	{
		var points = node.Positions;
		if ( points.Length != node.Children.Length ) return null;
		var weights = new float[points.Length];
		var inputLength = input.Length;
		// Freeform modes intersect gradient bands in Cartesian or polar space.
		for ( var i = 0; i < points.Length; i++ )
		{
			var origin = points[i];
			var originLength = origin.Length;
			var weight = 1f;
			for ( var j = 0; j < points.Length; j++ )
			{
				if ( i == j ) continue;
				var target = points[j];
				var edge = target - origin;
				var fromOrigin = input - origin;
				if ( directional )
				{
					var targetLength = target.Length;
					var meanLength = (originLength + targetLength) * .5f;
					if ( meanLength == 0 ) continue;
					var reference = originLength > 0 ? origin : target;
					var angularEdge = originLength > 0 && targetLength > 0 ? AngleBetween( origin, target ) : 0;
					edge = new( angularEdge * 2, (targetLength - originLength) / meanLength );
					fromOrigin = new( AngleBetween( reference, input ) * 2, (inputLength - originLength) / meanLength );
				}
				var square = edge.LengthSquared;
				if ( square > 0 ) weight = MathF.Min( weight, 1 - Vector2.Dot( fromOrigin, edge ) / square );
			}
			weights[i] = Math.Clamp( weight, 0, 1 );
		}
		var total = weights.Sum();
		if ( total <= 0 || !float.IsFinite( total ) ) return null;
		for ( var i = 0; i < weights.Length; i++ ) weights[i] /= total;
		return weights;
	}

	static float AngleBetween( Vector2 from, Vector2 to ) => MathF.Atan2( Cross( from, to ), Vector2.Dot( from, to ) );
	static float Cross( Vector2 a, Vector2 b ) => a.x * b.y - a.y * b.x;
}

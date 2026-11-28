namespace UnityMount;

sealed partial class UnityResourceRegistry
{
	string RegisterSpeedTreeModel( string group, string name, long id, UnityPrefabNode root )
	{
		var renderers = new Dictionary<UnityAssetPointer, Transform>();
		var groups = new List<(UnityPrefabLod Lod, Transform Local)>();
		var colliders = new List<UnityCompositeModelLoader.Collision>();
		Collect( root, Transform.Zero );
		if ( renderers.Count == 0 || groups.Count > 1 ) return null;

		var distances = new List<float> { 0f };
		var assignments = new Dictionary<UnityAssetPointer, List<int>>();
		var grouped = new HashSet<UnityAssetPointer>();
		if ( groups.Count == 1 )
		{
			var (lod, local) = groups[0];
			var scale = local.Scale.Abs();
			var size = lod.Size * MathF.Max( scale.x, MathF.Max( scale.y, scale.z ) );
			grouped.UnionWith( lod.Levels.SelectMany( level => level.Renderers ) );
			var nativeLevel = 0;
			for ( var i = 0; i < lod.Levels.Length && nativeLevel < 8; i++ )
			{
				var members = lod.Levels[i].Renderers.Where( renderers.ContainsKey ).ToArray();
				if ( members.Length == 0 ) continue;

				if ( nativeLevel > 0 )
				{
					var height = lod.Levels[i - 1].ScreenHeight;
					if ( height <= 0f ) break;
					// Unity uses projected height. Native models use distance, calibrated to a 60-degree vertical view.
					distances.Add( size / (2f * height * MathF.Tan( MathF.PI / 6f )) );
				}

				foreach ( var renderer in members )
				{
					if ( !assignments.TryGetValue( renderer, out var levels ) ) assignments.Add( renderer, levels = new() );
					levels.Add( nativeLevel );
				}
				nativeLevel++;
			}
		}

		var parts = new List<UnityCompositeModelLoader.Part>();
		foreach ( var (renderer, local) in renderers )
		{
			assignments.TryGetValue( renderer, out var levels );
			if ( levels is null && grouped.Contains( renderer ) ) continue;
			var source = _rendererSources[renderer];
			var visual = overrides.Renderers.GetValueOrDefault( renderer );
			if ( ResolveMesh( renderer.File, source.Mesh, visual ) is not { } mesh ) continue;
			var materials = ResolveMaterials( renderer.File, source.Materials, visual?.Material );
			parts.Add( new( mesh, materials.Select( material => material.Path ).ToArray(), local, levels?.ToArray() ?? [] ) );
		}
		if ( parts.Count == 0 || ReservePath( $"models/{group}/speedtrees/{name}", ".vmdl", id ) is not { } path ) return null;

		var geometry = parts.ToArray();
		var thresholds = distances.ToArray();
		var collision = colliders.ToArray();
		Define( ResourceType.Model, path, () => new UnityCompositeModelLoader( geometry, thresholds, collision ) );
		return ResourcePath( path );

		void Collect( UnityPrefabNode node, Transform local )
		{
			if ( !node.Enabled || local.Scale.x == 0f || local.Scale.y == 0f || local.Scale.z == 0f ) return;
			if ( node.Lod is { } lod ) groups.Add( (lod, local) );
			foreach ( var renderer in node.Renderers )
				if ( renderer is { Enabled: true, Skinned: false, Source: { } key } && _rendererSources.ContainsKey( key ) )
					renderers.TryAdd( key, local );
			foreach ( var collider in node.Colliders )
				if ( collider.Source is { Enabled: true, Trigger: false } )
					colliders.Add( new( collider.Source, local, collider.Mesh ) );
			foreach ( var child in node.Children ) Collect( child, local.ToWorld( child.Local ) );
		}
	}
}

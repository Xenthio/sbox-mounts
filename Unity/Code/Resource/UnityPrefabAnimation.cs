namespace UnityMount;

static class UnityPrefabAnimation
{
	public static void AddTo( GameObject root, UnityAnimationSource[] sources, bool animateTransforms,
		Func<GameObject, bool> isAuthored, Func<GameObject, bool> hasAnimator )
	{
		var (objects, paths, transformPaths) = FindTargets( root, isAuthored, animateTransforms, hasAnimator );

		var transforms = new List<UnityTransformAnimation.Clip>();
		var materials = new List<UnityMaterialAnimation.Clip>();
		var lights = new List<UnityLightClipPlayer.Clip>();
		foreach ( var source in sources )
		{
			if ( source.File.ReadAnimation( source.Asset ) is not { } clip ) continue;
			if ( transformPaths.Count > 0 && clip.Bind( transformPaths ) is { Length: > 0 } bindings )
			{
				var tracks = bindings.GroupBy( binding => binding.Joint ).Select( group =>
					BuildTrack( clip, objects[group.Key], group.Select( binding => binding.Binding ).ToArray() ) ).ToArray();
				transforms.Add( new( clip.Name, clip.Duration, clip.Rate, clip.Looping, tracks ) );
			}
			if ( UnityPrefabMaterialAnimation.BuildClip( clip, clip.Looping, paths, objects ) is { } materialClip )
				materials.Add( materialClip );
			if ( BuildLightClip( clip, clip.Looping, paths, objects ) is { } lightClip )
				lights.Add( lightClip );
		}

		if ( transforms.Count > 0 )
			root.AddComponent<UnityTransformAnimation>().Clips = transforms.ToArray();
		if ( materials.Count > 0 )
			root.AddComponent<UnityMaterialAnimation>().Clips = materials.ToArray();
		if ( lights.Count > 0 )
			root.AddComponent<UnityLightClipPlayer>().Clips = lights.ToArray();
	}

	public static UnityTimelinePlayer.LightCue[] BindTimelineLights( GameObject root, UnitySceneTimeline.ClipCue[] sources,
		Func<GameObject, bool> isAuthored )
	{
		var (objects, paths, _) = FindTargets( root, isAuthored, false, null );
		var cues = new List<UnityTimelinePlayer.LightCue>();
		foreach ( var source in sources )
		{
			if ( BuildLightClip( source.Clip, source.Looping, paths, objects ) is not { } clip ) continue;
			var timing = source.Timing;
			cues.Add( new( timing.Window.Start, timing.Window.End, timing.ClipIn, timing.Speed, timing.HoldStart, timing.HoldEnd, clip ) );
		}
		return cues.ToArray();
	}

	public static UnityTimelinePlayer.MaterialCue[] BindTimelineMaterials( GameObject root, UnitySceneTimeline.ClipCue[] sources,
		Func<GameObject, bool> isAuthored )
	{
		var (objects, paths, _) = FindTargets( root, isAuthored, false, null );
		var cues = new List<UnityTimelinePlayer.MaterialCue>();
		foreach ( var source in sources )
		{
			if ( UnityPrefabMaterialAnimation.BuildClip( source.Clip, source.Looping, paths, objects ) is not { } clip ) continue;
			var timing = source.Timing;
			cues.Add( new( timing.Window.Start, timing.Window.End, timing.ClipIn, timing.Speed, timing.HoldStart, timing.HoldEnd, clip ) );
		}
		return cues.ToArray();
	}

	public static UnityTimelinePlayer.ObjectCue[] BindTimelineObjects( GameObject root, UnitySceneTimeline.ObjectCue[] sources,
		Func<GameObject, bool> isAuthored )
	{
		var (objects, paths, _) = FindTargets( root, isAuthored, false, null );
		var cues = new List<UnityTimelinePlayer.ObjectCue>();
		foreach ( var source in sources )
		{
			var clip = source.Clip;
			var pose = source.RootOffset is { } offset
				? BuildTrack( clip, root, clip.RootBindings, offset, source.RelativeRoot ) : null;
			var visibility = new List<UnityTimelinePlayer.RendererVisibility>();
			foreach ( var binding in clip.VisibilityBindings )
			{
				if ( !paths.TryGetValue( binding.Path, out var index ) ) continue;
				var frames = clip.SampleScalarFrames( binding.Channel ).Select( value => value >= .5f ).ToArray();
				foreach ( var renderer in objects[index].GetComponents<Renderer>( true ) )
					if ( MatchesRenderer( renderer, binding.RendererClass ) ) visibility.Add( new( renderer, frames ) );
			}
			if ( pose is null && visibility.Count == 0 ) continue;
			var timing = source.Timing;
			cues.Add( new( timing.Window.Start, timing.Window.End, timing.ClipIn, timing.Speed, timing.HoldStart, timing.HoldEnd,
				clip.Duration, clip.Rate, source.Looping, pose, visibility.ToArray() ) );
		}
		return cues.ToArray();
	}

	internal static bool MatchesRenderer( Renderer renderer, int classId ) => classId switch
	{
		UnityLine.ClassId => renderer is LineRenderer,
		UnitySkin.RendererClassId => renderer is SkinnedModelRenderer,
		UnityMesh.RendererClassId => renderer is ModelRenderer and not SkinnedModelRenderer,
		_ => false
	};

	static UnityLightClipPlayer.Clip BuildLightClip( UnityAnimationClip clip, bool looping, Dictionary<uint, int> paths,
		List<GameObject> objects )
	{
		if ( clip.LightBindings.Count == 0 ) return null;
		var tracks = new List<UnityLightClipPlayer.Track>();
		foreach ( var binding in clip.LightBindings )
			if ( paths.TryGetValue( binding.Path, out var index ) && objects[index].GetComponent<Light>( true ) is { } light )
				tracks.Add( new( light, clip.SampleLight( binding ) ) );
		return tracks.Count > 0 ? new( clip.Name, clip.Duration, clip.Rate, looping, tracks.ToArray() ) : null;
	}

	static (List<GameObject> Objects, Dictionary<uint, int> Paths, Dictionary<uint, int> TransformPaths) FindTargets(
		GameObject root, Func<GameObject, bool> isAuthored, bool animateTransforms, Func<GameObject, bool> hasAnimator )
	{
		var objects = new List<GameObject> { root };
		var paths = new Dictionary<uint, int> { [0] = 0 };
		var transformPaths = new Dictionary<uint, int>();
		var ambiguous = new HashSet<uint>();
		Visit( root, "", animateTransforms );
		foreach ( var hash in ambiguous )
		{
			paths.Remove( hash );
			transformPaths.Remove( hash );
		}
		return (objects, paths, transformPaths);

		void Visit( GameObject parent, string parentPath, bool animate )
		{
			foreach ( var child in parent.Children )
			{
				if ( !isAuthored( child ) ) continue;
				var path = parentPath.Length == 0 ? child.Name : $"{parentPath}/{child.Name}";
				var hash = UnitySkin.HashPath( path );
				if ( !paths.TryAdd( hash, objects.Count ) ) ambiguous.Add( hash );
				var animateChild = animate && hasAnimator?.Invoke( child ) != true;
				if ( animateChild ) transformPaths.TryAdd( hash, objects.Count );
				objects.Add( child );
				Visit( child, path, animateChild );
			}
		}
	}

	static UnityTransformAnimation.Track BuildTrack( UnityAnimationClip clip, GameObject target,
		IReadOnlyList<UnityAnimationClip.Binding> bindings, Transform? rootOffset = null, bool relative = false )
	{
		var rest = target.LocalTransform;
		var frames = new Transform[(int)MathF.Ceiling( clip.Duration * clip.Rate ) + 1];
		for ( var i = 0; i < frames.Length; i++ )
		{
			var time = clip.TimeAtFrame( i );
			var pose = rootOffset is null ? rest : new Transform( Vector3.Zero, Rotation.Identity, rest.Scale );
			foreach ( var binding in bindings )
			{
				switch ( binding.Attribute )
				{
					case 1: pose.Position = UnityCoordinates.Position( clip.SampleVector( binding.Channel, time ) ); break;
					case 2 or 4: pose.Rotation = UnityCoordinates.RotationFor( clip.SampleRotation( binding, time ) ); break;
					case 3: pose.Scale = UnityCoordinates.Scale( clip.SampleVector( binding.Channel, time ) ); break;
				}
			}
			frames[i] = pose;
		}
		if ( rootOffset is { } offset )
		{
			var origin = relative ? frames[0].WithScale( 1f ) : global::Transform.Zero;
			for ( var i = 0; i < frames.Length; i++ )
				frames[i] = offset.ToWorld( origin.ToLocal( frames[i] ) );
		}
		return new( target, rest, frames );
	}
}

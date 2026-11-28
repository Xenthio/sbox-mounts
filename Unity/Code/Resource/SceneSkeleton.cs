namespace UnityMount;

static class UnitySceneSkeleton
{
	public static UnityTimelinePlayer.SkeletalTrack[] Bind( UnitySceneTimeline.SkeletalCue[] clips,
		IReadOnlyList<(SkinnedModelRenderer Renderer, UnityPrefabJoint[] Joints)> skins )
	{
		var tracks = new List<UnityTimelinePlayer.SkeletalTrack>();
		var drivers = new List<(SkinnedModelRenderer Renderer, HashSet<UnityPrefabJoint> Joints)>();
		foreach ( var (renderer, joints) in skins )
		{
			var cues = clips.Where( clip => renderer.Model.AnimationNames.Contains( clip.Name ) ).Select( clip =>
			{
				var timing = clip.Timing;
				return new UnityTimelinePlayer.SkeletalCue( timing.Window.Start, timing.Window.End, timing.ClipIn, timing.Speed,
					timing.HoldStart, timing.HoldEnd, clip.Name, clip.Looping );
			} ).ToArray();
			if ( cues.Length == 0 ) continue;

			tracks.Add( new( renderer, cues ) );
			if ( cues.Length == clips.Length ) drivers.Add( (renderer, joints.ToHashSet()) );
		}

		foreach ( var (renderer, joints) in skins )
		{
			if ( renderer.BoneMergeTarget.IsValid() || tracks.Any( track => track.Renderer == renderer ) ) continue;
			// A submesh can omit every animated parent while still sharing the scene's authored joints.
			foreach ( var driver in drivers.OrderByDescending( driver => driver.Renderer.Enabled && driver.Renderer.GameObject.Enabled )
				.ThenByDescending( driver => driver.Joints.Count ) )
			{
				if ( !driver.Joints.IsSupersetOf( joints ) ) continue;
				renderer.BoneMergeTarget = driver.Renderer;
				break;
			}
		}
		return tracks.ToArray();
	}
}

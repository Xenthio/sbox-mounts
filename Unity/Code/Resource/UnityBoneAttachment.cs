namespace UnityMount;

sealed class UnityPrefabBindings
{
	readonly Dictionary<GameObject, UnityAssetPointer> _nodes = new();
	readonly Dictionary<UnityAssetPointer, List<ModelRenderer>> _renderers = new();
	readonly List<(GameObject Owner, UnityPrefabLod Source)> _lods = new();
	readonly List<(SkinnedModelRenderer Renderer, UnityPrefabJoint[] Joints)> _skins = new();
	readonly Dictionary<UnityAssetPointer, List<(SkinnedModelRenderer Renderer, UnityPrefabJoint[] Joints)>> _timelineSkins = new();
	readonly HashSet<GameObject> _rigidObjects = new();
	readonly Dictionary<GameObject, (UnityAnimationSource[] Clips, bool AnimateTransforms)> _animations = new();
	readonly List<(UnityParticleWorldCollision Collision, UnityAssetPointer[] Planes)> _particlePlanes = new();
	readonly List<(UnityParticleWorldCollision Collision, UnityParticleCollisionBinding[] Emitters)> _collisionEmitters = new();
	readonly List<(GameObject Owner, UnityOmniElement[][] Groups)> _omniEffects = new();
	readonly Dictionary<(GameObject Node, UnityAssetPointer Material), Component> _particleRendererVariants = new();

	public void AddNode( UnityAssetPointer source, GameObject node ) => _nodes.Add( node, source );
	public void AddRenderer( UnityAssetPointer source, ModelRenderer renderer )
	{
		if ( !_renderers.TryGetValue( source, out var list ) ) _renderers.Add( source, list = new() );
		list.Add( renderer );
	}
	public void AddLod( GameObject owner, UnityPrefabLod source ) => _lods.Add( (owner, source) );
	public void AddSkin( SkinnedModelRenderer renderer, UnityPrefabJoint[] joints ) => _skins.Add( (renderer, joints) );
	public void AddTimelineSkin( UnityAssetPointer rig, SkinnedModelRenderer renderer, UnityPrefabJoint[] joints )
	{
		if ( !_timelineSkins.TryGetValue( rig, out var skins ) ) _timelineSkins.Add( rig, skins = new() );
		skins.Add( (renderer, joints) );
	}
	public void AddRigid( GameObject node ) => _rigidObjects.Add( node );
	public void AddAnimation( GameObject node, UnityAnimationSource[] clips, bool animateTransforms ) =>
		_animations.Add( node, (clips, animateTransforms) );
	public void AddParticlePlanes( UnityParticleWorldCollision collision, UnityAssetPointer[] planes ) => _particlePlanes.Add( (collision, planes) );
	public void AddCollisionEmitters( UnityParticleWorldCollision collision, UnityParticleCollisionBinding[] emitters ) =>
		_collisionEmitters.Add( (collision, emitters) );
	public void AddOmniEffect( GameObject owner, UnityOmniElement[][] groups ) => _omniEffects.Add( (owner, groups) );
	public void AddParticleRendererVariant( GameObject node, UnityAssetPointer material, Component renderer ) =>
		_particleRendererVariants.Add( (node, material), renderer );

	void ApplyTimelines( UnitySceneTimeline[] timelines, Func<UnityAssetPointer, GameObject> resolve )
	{
		foreach ( var timeline in timelines )
		{
			if ( resolve( timeline.Owner ) is not { } owner ) continue;
			var tracks = new List<UnityTimelinePlayer.ActivationTrack>();
			foreach ( var track in timeline.Tracks )
				if ( resolve( track.Target ) is { } target )
					tracks.Add( new( target, track.EndState, track.Cues.Select( cue => new UnityTimelinePlayer.Cue( cue.Start, cue.End ) ).ToArray() ) );
			var lights = new List<UnityTimelinePlayer.LightTrack>();
			foreach ( var track in timeline.Lights )
				if ( resolve( track.Target ) is { } root &&
					UnityPrefabAnimation.BindTimelineLights( root, track.Cues, _nodes.ContainsKey ) is { Length: > 0 } cues )
					lights.Add( new( owner.AddComponent<UnityLightClipPlayer>(), cues ) );
			var materials = new List<UnityTimelinePlayer.MaterialTrack>();
			foreach ( var track in timeline.Materials )
				if ( resolve( track.Target ) is { } root &&
					UnityPrefabAnimation.BindTimelineMaterials( root, track.Cues, _nodes.ContainsKey ) is { Length: > 0 } cues )
					materials.Add( new( owner.AddComponent<UnityMaterialAnimation>(), cues ) );
			var objects = new List<UnityTimelinePlayer.ObjectTrack>();
			foreach ( var track in timeline.Objects )
				if ( resolve( track.Target ) is { } root &&
					UnityPrefabAnimation.BindTimelineObjects( root, track.Cues, _nodes.ContainsKey ) is { Length: > 0 } cues )
					objects.Add( new( cues ) );
			var cameraTracks = new List<UnityTimelinePlayer.CameraTrack>();
			foreach ( var track in timeline.Cameras )
				if ( resolve( track.Target ) is { } target ) cameraTracks.Add( new( target, track.Cues ) );
			var sounds = new List<UnityTimelinePlayer.AudioCue>();
			foreach ( var source in timeline.Sounds )
				if ( resolve( source.Emitter ) is { } emitter )
					sounds.Add( new( emitter, source.Start, source.End, source.EventPath, source.StopAtEnd, source.OnceId ) );
			var skeletons = new List<UnityTimelinePlayer.SkeletalTrack>();
			foreach ( var track in timeline.Skeletons )
				if ( _timelineSkins.TryGetValue( track.Target, out var skins ) )
					skeletons.AddRange( UnitySceneSkeleton.Bind( track.Cues, skins ) );
			if ( tracks.Count == 0 && lights.Count == 0 && cameraTracks.Count == 0 && sounds.Count == 0 && skeletons.Count == 0 && materials.Count == 0 && objects.Count == 0 )
				continue;

			var player = owner.AddComponent<UnityTimelinePlayer>();
			player.TimelineName = timeline.Name;
			player.Duration = timeline.Duration;
			player.InitialTime = timeline.InitialTime;
			player.WrapMode = timeline.WrapMode;
			player.ActivationTracks = tracks.ToArray();
			player.LightTracks = lights.ToArray();
			player.MaterialTracks = materials.ToArray();
			player.ObjectTracks = objects.ToArray();
			player.CameraTracks = cameraTracks.ToArray();
			player.AudioCues = sounds.ToArray();
			player.SkeletalTracks = skeletons.ToArray();
		}
	}

	static void ApplyCameras( UnitySceneCamera[] cameras, Func<UnityAssetPointer, GameObject> resolve )
	{
		var shots = new Dictionary<UnityAssetPointer, UnityCameraShot>();
		foreach ( var source in cameras )
		{
			if ( resolve( source.Transform ) is not { } target ) continue;
			var shot = target.AddComponent<UnityCameraShot>();
			shot.VerticalFieldOfView = source.FieldOfView;
			shot.EntryTime = source.EntryTime;
			shot.ExitTime = source.ExitTime;
			shot.Rail = source.Rail;
			shot.RailPosition = source.RailPosition;
			shot.LookAtTarget = source.LookAt is { } lookAt ? resolve( lookAt ) : null;
			shots.Add( source.Transform, shot );
		}
		foreach ( var source in cameras )
			if ( source.Next is { } next && shots.TryGetValue( source.Transform, out var shot ) )
				shot.Next = shots.GetValueOrDefault( next );
	}

	public void Apply( UnitySceneTimeline[] timelines = null, UnitySceneCamera[] cameras = null )
	{
		ApplyBoneAttachments();
		ApplyLods();
		foreach ( var (root, animation) in _animations )
			UnityPrefabAnimation.AddTo( root, animation.Clips, animation.AnimateTransforms,
				_nodes.ContainsKey, _animations.ContainsKey );
		if ( _particlePlanes.Count == 0 && _collisionEmitters.Count == 0 && _omniEffects.Count == 0 &&
			timelines is not { Length: > 0 } && cameras is not { Length: > 0 } ) return;
		var bySource = _nodes.ToLookup( node => node.Value, node => node.Key );
		foreach ( var (owner, groups) in _omniEffects )
		{
			var player = owner.AddComponent<UnityOmniEffect>();
			player.Groups = groups.Select( group => new UnityOmniEffect.Group( group.Select( BindElement ).ToArray() ) ).ToArray();

			UnityOmniEffect.Element BindElement( UnityOmniElement element )
			{
				var node = element.Transform is { } pointer ? bySource[pointer].FirstOrDefault( owner.IsDescendant ) : null;
				var renderer = node is not null && element.Material is { } material
					? _particleRendererVariants.GetValueOrDefault( (node, material) ) : null;
				return new( node, element.Emit, float.IsFinite( element.MaximumRadius ) ? element.MaximumRadius : null,
					element.BonusEmission, renderer );
			}
		}
		if ( cameras is { Length: > 0 } ) ApplyCameras( cameras, Resolve );
		if ( timelines is { Length: > 0 } ) ApplyTimelines( timelines, Resolve );
		foreach ( var (collision, planes) in _particlePlanes )
			collision.Planes = planes.Select( Resolve ).OfType<GameObject>().ToArray();
		foreach ( var (collision, emitters) in _collisionEmitters )
		{
			var targets = new List<UnityParticleWorldCollision.SubEmitter>();
			foreach ( var source in emitters )
			{
				var target = Resolve( source.Node );
				if ( target == collision.GameObject || target?.GetComponent<UnityShapeEmitter>( true ) is not { Bursts.Length: > 0 } emitter ) continue;
				emitter.CollisionOnly = true;
				emitter.Rate = 0;
				emitter.RateOverDistance = 0;
				emitter.Enabled = true;
				targets.Add( new( emitter, source.Probability, source.InheritRotation ) );
			}
			collision.SubEmitters = targets.ToArray();
		}

		GameObject Resolve( UnityAssetPointer asset )
		{
			var matches = bySource[asset].Take( 2 ).ToArray();
			return matches.Length == 1 ? matches[0] : null;
		}
	}

	void ApplyLods()
	{
		foreach ( var (owner, source) in _lods )
		{
			var levels = source.Levels.Select( level => new UnityLodGroup.Level( level.ScreenHeight,
				level.Renderers.Select( ResolveRenderer ).OfType<ModelRenderer>().Distinct().ToArray() ) ).ToArray();
			var firstSupported = Array.FindIndex( levels, level => level.Renderers.Length > 0 );
			if ( firstSupported < 0 ) continue;

			// Keep the closest supported mesh when an authored level uses an unavailable renderer.
			var fallback = levels[firstSupported].Renderers;
			for ( var i = 0; i < levels.Length; i++ )
			{
				if ( levels[i].Renderers.Length > 0 )
					fallback = levels[i].Renderers;
				else if ( source.Levels[i].HasAuthoredRenderers )
					levels[i] = levels[i] with { Renderers = fallback };
			}
			var component = owner.AddComponent<UnityLodGroup>( false );
			component.ReferencePoint = source.Center;
			component.Size = source.Size;
			component.Levels = levels;
			component.Enabled = true;

			ModelRenderer ResolveRenderer( UnityAssetPointer id )
			{
				if ( !_renderers.TryGetValue( id, out var candidates ) ) return null;
				if ( candidates.Count == 1 ) return candidates[0];
				var local = candidates.Where( renderer => BelongsTo( renderer.GameObject, owner ) ).Take( 2 ).ToArray();
				return local.Length == 1 ? local[0] : null;
			}
		}

		static bool BelongsTo( GameObject child, GameObject owner )
		{
			for ( var current = child; current.IsValid(); current = current.Parent )
				if ( current == owner ) return true;
			return false;
		}
	}

	void ApplyBoneAttachments()
	{
		if ( _skins.Count == 0 ) return;

		var joints = new Dictionary<UnityAssetPointer, (SkinnedModelRenderer Renderer, string Bone)>();
		foreach ( var (renderer, references) in _skins.OrderByDescending( skin => skin.Renderer.Enabled )
			.ThenByDescending( skin => skin.Joints.Length ).ThenByDescending( skin => skin.Renderer.Model.Bounds.Volume ) )
		{
			foreach ( var joint in references )
				joints.TryAdd( joint.Transform, (renderer, joint.Name) );
		}

		foreach ( var target in _rigidObjects )
			Attach( target, null );
		foreach ( var (renderer, references) in _skins )
			Attach( renderer.GameObject, references );

		void Attach( GameObject target, UnityPrefabJoint[] ownJoints )
		{
			if ( target.GetComponent<UnityBoneAttachment>( true ) is not null ) return;
			for ( var parent = target; parent is not null && _nodes.TryGetValue( parent, out var source ); parent = parent.Parent )
			{
				if ( !joints.TryGetValue( source, out var binding ) ) continue;
				// A skin already follows its own joints. A separate rig may hang from a joint of another model.
				if ( ownJoints?.Any( joint => joint.Transform == source ) == true ) break;
				if ( binding.Renderer.GameObject.IsAncestor( target ) ) return;
				var bone = binding.Renderer.Model.Bones.GetBone( binding.Bone );
				if ( bone is null ) break;

				var inModel = binding.Renderer.WorldTransform.ToLocal( target.WorldTransform );
				var attachment = target.AddComponent<UnityBoneAttachment>();
				attachment.Renderer = binding.Renderer;
				attachment.Bone = binding.Bone;
				attachment.Offset = bone.LocalTransform.ToLocal( inModel );
				break;
			}
		}
	}
}

public sealed class UnityBoneAttachment : Component
{
	[Property] public SkinnedModelRenderer Renderer { get; set; }
	[Property] public string Bone { get; set; }
	[Property] public Transform Offset { get; set; }

	internal void UpdateTransform()
	{
		if ( !Renderer.IsValid() || Renderer.SceneModel is not { } model || !model.IsValid() ||
			Renderer.Model?.Bones.GetBone( Bone ) is not { } bone ) return;

		var pose = model.GetBoneWorldTransform( bone.Index ).ToWorld( Offset );
		if ( WorldTransform != pose ) WorldTransform = pose;
	}
}

public sealed class UnityBoneAttachmentSystem : GameObjectSystem<UnityBoneAttachmentSystem>
{
	public UnityBoneAttachmentSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.UpdateBones, 10, UpdateAttachments, "Update Unity bone attachments" );
		Listen( Stage.PrepareRender, 0, UpdateAttachments, "Prepare Unity bone attachments" );
	}

	void UpdateAttachments()
	{
		foreach ( var attachment in Scene.GetAllComponents<UnityBoneAttachment>() )
			if ( attachment.Active ) attachment.UpdateTransform();
	}
}

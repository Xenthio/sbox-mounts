namespace UnityMount;

sealed record UnityPrefabNode( string Name, bool Enabled, Transform Local, uint Layer, UnityPrefabRenderer[] Renderers,
	UnityLight[] Lights, UnityPrefabParticle Particle, UnityPrefabCollider[] Colliders, UnityPrefabNode[] Children, bool SpawnPoint,
	UnitySceneProbe? ReflectionProbe, UnityPrefabSprite? Sprite, UnityAssetPointer Source, UnityPrintAnimationData? Print = null,
	UnityAnimationSource[] AnimationClips = null, bool AnimateTransforms = false, UnityPrefabLine Line = null, UnityPrefabLod Lod = null,
	string[] Tags = null, UnityOmniElement[][] OmniEffect = null, UnityLocalSpinData? Spin = null );

readonly record struct UnityPrefabJoint( UnityAssetPointer Transform, string Name );
readonly record struct UnityPrefabRenderer( string Path, bool Skinned, bool Enabled, Transform? WorldTransform = null,
	UnityPrefabJoint[] Joints = null, UnityAssetPointer? TimelineRig = null, UnityAssetPointer? Source = null,
	int BillboardImages = 0, bool BillboardShadows = false );

sealed class UnityPrefabLoader( UnityPrefabNode source, string preview, string title = null ) : ResourceLoader<GameMount>
{
	PrefabFile _prefab;

	protected override object Load()
	{
		var builder = new PrefabBuilder().WithName( Path )
			.WithMetadata( "Title", title ?? source.Name )
			.WithMetadata( "Group", Host.Title )
			.WithMetadata( "PreviewModel", preview );

		using ( builder.Scope() )
		{
			Game.ActiveScene.Enabled = false;
			CreateObject( source, null );
		}

		return _prefab = builder.Create();
	}

	internal static void CreateObject( UnityPrefabNode node, GameObject parent, bool isStatic = false )
	{
		var bindings = new UnityPrefabBindings();
		CreateHierarchy( node, parent, isStatic, bindings );
		bindings.Apply();
	}

	internal static void CreateSceneObjects( UnityPrefabNode[] roots, UnitySceneTimeline[] timelines, UnitySceneCamera[] cameras )
	{
		var bindings = new UnityPrefabBindings();
		foreach ( var root in roots ) CreateHierarchy( root, null, true, bindings );
		bindings.Apply( timelines, cameras );
	}

	static void CreateHierarchy( UnityPrefabNode node, GameObject parent, bool isStatic, UnityPrefabBindings bindings )
	{
		var gameObject = new GameObject( parent, node.Enabled, node.Name ) { LocalTransform = node.Local, IsStatic = isStatic };
		if ( node.Tags is { Length: > 0 } ) gameObject.Tags.Add( node.Tags );
		bindings.AddNode( node.Source, gameObject );
		foreach ( var sourceRenderer in node.Renderers )
		{
			var model = Model.Load( sourceRenderer.Path );
			if ( model is null || model.IsError ) continue;

			var target = gameObject;
			if ( sourceRenderer.WorldTransform is { } world )
				target = new GameObject( gameObject, true, "Static batch" ) { WorldTransform = world, IsStatic = isStatic };
			else if ( sourceRenderer.BillboardImages > 0 )
				target = new GameObject( gameObject, true, "Billboard" );

			ModelRenderer renderer;
			if ( sourceRenderer.Skinned )
			{
				var skinned = target.AddComponent<SkinnedModelRenderer>( false );
				skinned.UseAnimGraph = false;
				renderer = skinned;
			}
			else
			{
				renderer = target.AddComponent<ModelRenderer>( false );
			}
			renderer.Model = model;
			if ( sourceRenderer.BillboardImages > 0 )
			{
				renderer.RenderType = sourceRenderer.BillboardShadows ? ModelRenderer.ShadowRenderType.On : ModelRenderer.ShadowRenderType.Off;
				var billboard = target.AddComponent<UnityBillboardRenderer>( false );
				billboard.Renderer = renderer;
				billboard.ImageCount = sourceRenderer.BillboardImages;
				billboard.Enabled = true;
			}
			renderer.Enabled = sourceRenderer.Enabled;
			if ( sourceRenderer.Source is { } id ) bindings.AddRenderer( id, renderer );
			if ( sourceRenderer.TimelineRig is { } rig && renderer is SkinnedModelRenderer animated )
				bindings.AddTimelineSkin( rig, animated, sourceRenderer.Joints );
			if ( !isStatic && sourceRenderer.Joints is { Length: > 0 } joints && renderer is SkinnedModelRenderer skin )
				bindings.AddSkin( skin, joints );
			else if ( !isStatic && renderer is not SkinnedModelRenderer && sourceRenderer.WorldTransform is null )
				bindings.AddRigid( gameObject );
		}

		node.Line?.Create( gameObject );

		foreach ( var sourceLight in node.Lights )
		{
			Light light;
			if ( sourceLight.Spot is { } cone )
			{
				var spot = gameObject.AddComponent<SpotLight>( false );
				spot.Radius = sourceLight.Radius;
				spot.ConeInner = cone.Inner;
				spot.ConeOuter = cone.Outer;
				light = spot;
			}
			else if ( sourceLight.Directional )
			{
				light = gameObject.AddComponent<DirectionalLight>( false );
			}
			else
			{
				var point = gameObject.AddComponent<PointLight>( false );
				point.Radius = sourceLight.Radius;
				light = point;
			}
			light.LightColor = sourceLight.Tint;
			light.Intensity = sourceLight.Intensity;

			light.Shadows = sourceLight.Shadows;
			light.Temperature = sourceLight.Temperature;
			light.Enabled = sourceLight.Enabled;
			if ( light is not PointLight animatedLight || sourceLight.Animation is not { } animation ) continue;

			var player = gameObject.AddComponent<UnityLightAnimation>( false );
			player.Light = animatedLight;
			player.Intensity = animation.Intensity;
			player.PeakIntensity = sourceLight.Intensity;
			player.Duration = animation.Duration;
			player.Loop = animation.Loop;
			player.RandomStart = animation.RandomStart;
			player.Enabled = animation.Enabled;
		}

		if ( node.Spin is { } spin )
		{
			var player = gameObject.AddComponent<UnityLocalSpin>( false );
			player.Speed = spin.Maximum;
			player.RandomizeSpeed = spin.Minimum.HasValue;
			player.MinimumSpeed = spin.Minimum.GetValueOrDefault();
			player.Enabled = true;
		}

		node.Particle?.AddTo( gameObject, bindings );
		node.Sprite?.AddTo( gameObject );

		if ( node.Colliders.Length > 0 && node.Layer < 32 ) gameObject.Tags.Add( UnitySceneObjects.LayerTag( (int)node.Layer ) );
		foreach ( var collider in node.Colliders ) collider.AddTo( gameObject );
		if ( node.SpawnPoint ) gameObject.AddComponent<SpawnPoint>();
		node.ReflectionProbe?.AddTo( gameObject );

		foreach ( var child in node.Children )
			CreateHierarchy( child, gameObject, isStatic, bindings );
		if ( node.Lod is { } lod ) bindings.AddLod( gameObject, lod );
		if ( node.OmniEffect is { } omniEffect ) bindings.AddOmniEffect( gameObject, omniEffect );
		if ( node.AnimationClips is { Length: > 0 } clips ) bindings.AddAnimation( gameObject, clips, node.AnimateTransforms );
		if ( node.Print is { } print )
		{
			var player = gameObject.AddComponent<UnityPrintAnimation>( false );
			player.Curve = print.Curve;
			player.Duration = print.Duration;
			player.Height = print.Height;
			player.Bias = print.Bias;
			player.FlowPower = print.FlowPower ?? new( -1, -1 );
			player.PlayOnStart = print.PlayOnStart;
			player.Paused = print.Paused;
			player.DisableWhenFinished = print.DisableWhenFinished;
			player.IgnoreNestedControllers = print.IgnoreNestedControllers;
			player.Enabled = true;
		}
	}

	protected override void Shutdown()
	{
		PrefabBuilder.Destroy( _prefab );
		_prefab = null;
		base.Shutdown();
	}
}

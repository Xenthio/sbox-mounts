using Sandbox.Rendering;
using Sandbox.Volumes;

namespace UnityMount;

sealed record UnitySceneContent( UnityPrefabNode[] Roots, UnitySceneSky? Sky, UnitySceneFog? Fog, UnitySceneProbe? Reflection,
	Color? Ambient, IReadOnlyDictionary<string, UnityResourceDefinition> Resources, UnityStaticBatchCache StaticBatches,
	UnitySceneTimeline[] Timelines, UnitySceneCamera[] Cameras );

sealed class UnitySceneLoader : SceneLoader<GameMount>
{
	readonly Lazy<UnitySceneMarkers> _markers;
	readonly Lazy<UnitySceneContent> _content;

	public UnitySceneLoader( Func<UnitySceneMarkers> readMarkers, Func<UnitySceneMarkers, UnitySceneContent> prepare )
	{
		_markers = new( readMarkers );
		_content = new( () => prepare( _markers.Value ) );
	}

	protected override ResourceLoader ResolveChild( string name, out ResourceType type )
	{
		type = default;
		if ( !name.Contains( '/' ) ) return null;
		try
		{
			// Scene metadata can expose resources before the scene's models and hierarchy have been read.
			var resource = _markers.Value.Resources?.GetValueOrDefault( name ) ?? _content.Value.Resources.GetValueOrDefault( name );
			if ( resource is null ) return null;
			type = resource.Type;
			return resource.Create();
		}
		catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
		{
			return null;
		}
	}

	protected override void BuildScene()
	{
		// Components only need to activate in the scene that loads the finished resource.
		Game.ActiveScene.Enabled = false;
		var content = _content.Value;
		content.Sky?.AddToScene();
		if ( content.Ambient is { } color )
		{
			var gameObject = new GameObject( true, "Environment ambient light" ) { IsStatic = true };
			// Alpha selects flat ambient in the renderer without changing the specular probes.
			gameObject.AddComponent<AmbientLight>().Color = UnityColor.ToLinear( color ).WithAlpha( 1 );
		}
		if ( content.Fog is { } layer ) AddFog( layer );
		if ( content.Reflection is { } probe ) probe.AddTo( new GameObject( true, "Environment reflection" ) { IsStatic = true } );
		content.StaticBatches.Begin();
		try
		{
			UnityPrefabLoader.CreateSceneObjects( content.Roots, content.Timelines, content.Cameras );
		}
		finally
		{
			content.StaticBatches.End();
		}
	}

	static void AddFog( UnitySceneFog source )
	{
		var gameObject = new GameObject( true, "Environment fog" ) { IsStatic = true };
		var volume = gameObject.AddComponent<PostProcessVolume>();
		volume.SceneVolume = new SceneVolume { Type = SceneVolume.VolumeTypes.Infinite };
		volume.BlendDistance = 0;

		var fog = gameObject.AddComponent<Fog>( false );
		fog.DistanceMetric = FogDistanceMetric.ViewDepth;
		fog.ColorSource = FogColorSource.Constant;
		fog.Color = source.Color;
		fog.SkyAffect = 0;
		if ( source.AttenuationDistance > 0 )
		{
			fog.Mode = FogMode.Exponential;
			fog.AttenuationDistance = source.AttenuationDistance;
		}
		else
		{
			fog.Mode = FogMode.Distance;
			fog.DistanceRange = new( source.Start, source.End );
			fog.MaxDistance = MathF.Max( fog.MaxDistance, source.End );
		}
		fog.Enabled = true;
	}
}

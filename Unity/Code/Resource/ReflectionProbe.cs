namespace UnityMount;

readonly record struct UnitySceneProbe( UnityReflectionProbe Source, string Cubemap )
{
	public void AddTo( GameObject gameObject )
	{
		var texture = Texture.Load( Cubemap );
		if ( texture is null || texture.IsError ) return;

		var probe = gameObject.AddComponent<EnvmapProbe>( false );
		probe.Mode = EnvmapProbe.EnvmapProbeMode.CustomTexture;
		probe.Texture = texture;
		probe.Bounds = Source.Bounds;
		probe.Priority = Source.Priority;
		probe.TintColor = new Color( Source.Intensity, Source.Intensity, Source.Intensity );
		probe.Projection = Source.BoxProjection ? SceneCubemap.ProjectionMode.Box : SceneCubemap.ProjectionMode.Sphere;
		// Negative feathering places the fade inside the probe's influence bounds.
		probe.Feathering = -Source.BlendDistance;
		probe.Enabled = Source.Enabled;
	}
}

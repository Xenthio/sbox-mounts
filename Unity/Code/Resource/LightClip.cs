namespace UnityMount;

/// <summary>A mounted Unity clip's intensity tracks, addressed by relative Unity path hash.</summary>
[AssetType( Name = "Unity Light Clip", Extension = "unitylight" )]
public sealed class UnityLightClip : GameResource
{
	public sealed record Track( uint PathHash, float[] Frames );

	public string ClipName { get; set; }
	public float Duration { get; set; }
	public float SampleRate { get; set; }
	public bool Looping { get; set; }
	public Track[] Tracks { get; set; } = [];
}

sealed class UnityLightClipLoader( UnityAnimationClip clip ) : ResourceLoader<GameMount>
{
	protected override object Load() => new UnityLightClip
	{
		ClipName = clip.Name,
		Duration = clip.Duration,
		SampleRate = clip.Rate,
		Looping = clip.Looping,
		Tracks = clip.LightBindings.Select( binding => new UnityLightClip.Track( binding.Path, clip.SampleLight( binding ) ) ).ToArray()
	};
}

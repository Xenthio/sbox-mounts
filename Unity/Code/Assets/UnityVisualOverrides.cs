namespace UnityMount;

sealed class UnityVisualOverrides
{
	public UnityAssetPointer? SnowMicrofacetNoise { get; set; }

	public sealed record Skin( UnityAssetPointer Model, string Name, UnityVisualOverrides Visuals );
	public Dictionary<UnityAssetPointer, Skin[]> Skins { get; } = new();
	public sealed record Renderer( UnityAssetPointer? Mesh, UnityAssetPointer? Material );
	public Dictionary<UnityAssetPointer, Renderer> Renderers { get; } = new();
	public Dictionary<UnityAssetPointer, UnityAssetPointer> Controllers { get; } = new();
	public Dictionary<string, string> InPlaceAnimations { get; } = new( StringComparer.Ordinal );
	public readonly record struct Attachment( UnityAssetPointer Prefab, Transform Local );
	public Dictionary<UnityAssetPointer, List<Attachment>> Attachments { get; } = new();
	public Dictionary<UnityAssetPointer, string[]> ObjectTags { get; } = new();
	public Dictionary<UnityAssetPointer, bool> Activations { get; } = new();
	public Dictionary<UnityAssetPointer, UnityLocalSpinData> LocalSpins { get; } = new();
	public Dictionary<UnityAssetPointer, UnityLightAnimationData> LightAnimations { get; } = new();
	public Dictionary<UnityAssetPointer, UnityPrintAnimationData> PrintAnimations { get; } = new();
	public readonly record struct EffectAttachment( bool AlignToSocket, bool FollowsSocket );
	public Dictionary<UnityAssetPointer, EffectAttachment> EffectAttachments { get; } = new();
	public Dictionary<UnityAssetPointer, UnityOmniElement[][]> OmniEffects { get; } = new();
	public Dictionary<UnityAssetPointer, HashSet<UnityAssetPointer>> OmniEffectMaterials { get; } = new();
}

using System.Text.Json.Serialization;

namespace UnityMount;

readonly record struct UnityAnimationEventPrefab( string Path, UnityVisualOverrides.EffectAttachment Attachment );

[Sandbox.ModelEditor.GameData( "unity_animation_events", AllowMultiple = true ), Hide]
public sealed class UnityAnimationEventData
{
	public string Sequence { get; set; }
	public float Time { get; set; }
	[JsonNumberHandling( JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString )]
	public float? ClipPhase { get; set; }
	public string Function { get; set; }
	public string Data { get; set; }
	public string Prefab { get; set; }
	public bool AlignToSocket { get; set; }
	public bool FollowsSocket { get; set; }
	public float FloatParameter { get; set; }
	public int IntParameter { get; set; }
	public int MessageOptions { get; set; }
}

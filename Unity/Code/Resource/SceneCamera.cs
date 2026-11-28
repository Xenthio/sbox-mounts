namespace UnityMount;

sealed record UnityAuthoredCamera( UnityAssetPointer Component, float FieldOfView, float EntryTime, float ExitTime,
	UnityAssetPointer? NextComponent, UnityAssetPointer? LookAt, UnityCameraRail Rail = null );

sealed record UnitySceneCamera( UnityAssetPointer Transform, float FieldOfView, float EntryTime, float ExitTime,
	UnityAssetPointer? Next, UnityAssetPointer? LookAt, UnityCameraShot.RailSample[] Rail, float RailPosition );

sealed record UnityCameraRail( UnityCameraRail.Control[] Controls, int SamplesPerSegment, float Position )
{
	public readonly record struct Control( UnityAssetPointer Transform, Vector3 Forward, Vector3 Backward );
}

namespace UnityMount;

readonly record struct UnityCameraPose( Vector3 Position, Rotation Rotation, float VerticalFov )
{
	public void Apply( CameraComponent camera )
	{
		camera.WorldPosition = Position;
		camera.WorldRotation = Rotation;
		if ( VerticalFov > 0 )
		{
			camera.FovAxis = CameraComponent.Axis.Vertical;
			camera.FieldOfView = VerticalFov;
		}
	}
}

/// <summary>An authored scene camera pose that can be applied to a native camera.</summary>
[Title( "Mounted Camera Shot" )]
public sealed class UnityCameraShot : Component
{
	public readonly record struct RailSample( Vector3 Position, float Distance );

	[Property] public float VerticalFieldOfView { get; set; }
	[Property] public float EntryTime { get; set; }
	[Property] public float ExitTime { get; set; }
	[Property] public GameObject LookAtTarget { get; set; }
	[Property] public UnityCameraShot Next { get; set; }
	[Property, Hide] public RailSample[] Rail { get; set; } = [];
	[Property, Hide] public float RailPosition { get; set; }

	public void ApplyTo( CameraComponent camera )
	{
		if ( !camera.IsValid() ) return;
		GetPose( RailPosition, VerticalFieldOfView, null ).Apply( camera );
	}

	internal UnityCameraPose GetPose( float railPosition, float verticalFov, Vector3? lookAt )
	{
		var position = Rail.Length > 1 ? SampleRail( railPosition ) : GameObject.WorldPosition;
		var rotation = GameObject.WorldRotation;
		if ( lookAt is null && LookAtTarget.IsValid() ) lookAt = LookAtTarget.WorldPosition;
		if ( lookAt is { } target )
		{
			var direction = target - position;
			if ( direction.LengthSquared > 0 ) rotation = Rotation.LookAt( direction, Vector3.Up );
		}
		return new( position, rotation, verticalFov );
	}

	Vector3 SampleRail( float position )
	{
		var distance = Math.Clamp( position, 0, 1 ) * Rail[^1].Distance;
		var lo = 0;
		var hi = Rail.Length - 1;
		while ( lo + 1 < hi )
		{
			var mid = (lo + hi) / 2;
			if ( Rail[mid].Distance < distance ) lo = mid;
			else hi = mid;
		}
		var from = Rail[lo];
		var to = Rail[hi];
		var length = to.Distance - from.Distance;
		return length > 0 ? Vector3.Lerp( from.Position, to.Position, (distance - from.Distance) / length ) : from.Position;
	}
}

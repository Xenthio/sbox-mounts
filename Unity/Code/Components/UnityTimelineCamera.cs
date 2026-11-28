namespace UnityMount;

/// <summary>Blends a mounted Timeline and a live gameplay view into a separate output camera.</summary>
[Title( "Mounted Timeline Camera" )]
public sealed class UnityTimelineCamera : Component, Component.DontExecuteOnServer
{
	[Property] public UnityTimelinePlayer Timeline { get; set; }
	[Property] public CameraComponent GameplayCamera { get; set; }
	[Property] public CameraComponent OutputCamera { get; set; }

	IDisposable _hook;
	UnityTimelinePlayer _timeline;
	CameraComponent _gameplay;
	CameraComponent _output;
	UnityCameraShot _shot;
	UnityCameraPose _current;
	UnityCameraPose _from;
	float _elapsed;
	float _duration;

	protected override void OnEnabled()
	{
		_hook = Scene.AddHook( GameObjectSystem.Stage.FinishUpdate, 1, UpdateView, nameof( UnityTimelineCamera ), "Update Timeline camera" );
	}

	protected override void OnDisabled()
	{
		_hook?.Dispose();
		_hook = null;
		ReleaseView();
	}

	void UpdateView()
	{
		if ( _timeline != Timeline || _output != OutputCamera || _gameplay != GameplayCamera )
		{
			ReleaseView();
			if ( !Timeline.IsValid() || Timeline.Scene != Scene ||
				!GameplayCamera.IsValid() || GameplayCamera.Scene != Scene ||
				!OutputCamera.IsValid() || OutputCamera.Scene != Scene ||
				!CanCopy( GameplayCamera, OutputCamera ) ) return;

			_timeline = Timeline;
			_output = OutputCamera;
			_gameplay = GameplayCamera;
			_current = GameplayPose();
		}
		if ( !_timeline.IsValid() || !CanCopy( _gameplay, _output ) )
		{
			ReleaseView();
			return;
		}

		var shot = _timeline.CurrentCameraShot;
		if ( _shot != shot )
		{
			_from = _current;
			_elapsed = 0;
			_duration = _shot.IsValid() && (shot is null || _shot.Next == shot)
				? _shot.ExitTime : shot?.EntryTime ?? 0;
			_shot = shot;
		}

		var gameplay = GameplayPose();
		var target = _shot.IsValid() ? _timeline.GetCameraPose( _shot ) : gameplay;
		if ( !float.IsFinite( target.VerticalFov ) || target.VerticalFov <= 0 )
			target = target with { VerticalFov = gameplay.VerticalFov };
		_elapsed = Math.Min( _duration, _elapsed + Time.Delta );
		if ( _elapsed < _duration )
		{
			var t = _elapsed / _duration;
			// The source rig remaps its entry and exit time with quadratic ease-in-out.
			var amount = t < .5f ? 2 * t * t : 1 - 2 * (1 - t) * (1 - t);
			_current = new( Vector3.Lerp( _from.Position, target.Position, amount ),
				Rotation.Slerp( _from.Rotation, target.Rotation, amount ),
				MathX.Lerp( _from.VerticalFov, target.VerticalFov, amount ) );
			_current.Apply( _output );
		}
		else
		{
			_current = target;
			if ( _shot.IsValid() ) _current.Apply( _output );
			else CopyGameplay();
		}
	}

	UnityCameraPose GameplayPose()
	{
		var fov = _gameplay.FieldOfView;
		if ( _gameplay.FovAxis == CameraComponent.Axis.Horizontal )
		{
			var rect = _output.ScreenRect;
			var inverseAspect = rect.Width > 0 && rect.Height > 0 ? rect.Height / rect.Width : 1f / Screen.Aspect;
			fov = Screen.CreateVerticalFieldOfView( fov, inverseAspect );
		}
		return new( _gameplay.WorldPosition, _gameplay.WorldRotation, fov );
	}

	void CopyGameplay()
	{
		_output.WorldPosition = _gameplay.WorldPosition;
		_output.WorldRotation = _gameplay.WorldRotation;
		_output.FovAxis = _gameplay.FovAxis;
		_output.FieldOfView = _gameplay.FieldOfView;
	}

	void ReleaseView()
	{
		if ( CanCopy( _gameplay, _output ) ) CopyGameplay();
		_timeline = null;
		_gameplay = null;
		_output = null;
		_shot = null;
		_elapsed = 0;
		_duration = 0;
	}

	static bool CanCopy( CameraComponent gameplay, CameraComponent output ) =>
		gameplay.IsValid() && output.IsValid() && gameplay != output && !gameplay.Orthographic && !output.Orthographic;
}

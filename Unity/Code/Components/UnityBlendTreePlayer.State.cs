namespace UnityMount;

public sealed partial class UnityBlendTreePlayer
{
	/// <summary>Plays a named state on an active layer. The fade is in seconds and the destination phase is normalized.</summary>
	public bool SetControllerState( string layerName, string statePath, float fadeDuration = 0, float destinationPhase = 0 )
	{
		if ( _controller is null || string.IsNullOrEmpty( statePath ) || !float.IsFinite( fadeDuration ) ||
			fadeDuration < 0 || !float.IsFinite( destinationPhase ) || !CanRender() ) return false;

		var index = Array.FindIndex( _controller.Layers, layer => layer.Name == layerName );
		if ( FindLayer( index ) is not { Frozen: false, SynchronizedSource: < 0 } playback ) return false;
		var state = FindState( playback.Machine, statePath );
		if ( state < 0 ) return false;
		var destination = new StateTime( state, destinationPhase );
		if ( !CanPlayState( playback, destination ) ) return false;

		var current = playback.Current;
		var previous = playback.Fade;
		var synchronized = previous is not null && fadeDuration > 0 ? FindSynchronizedLayers( index ) : [];
		var previousSnapshots = synchronized.Select( layer => layer.SynchronizedSnapshot ).ToArray();
		LayerSnapshot? snapshot = null;
		if ( fadeDuration > 0 && previous is { } interrupted )
		{
			snapshot = CaptureInterruptedPose( playback, synchronized, current, interrupted );
			if ( snapshot is null ) return false;
		}

		if ( fadeDuration > 0 ) playback.Fade = new( destination, null, fadeDuration, Snapshot: snapshot );
		else
		{
			playback.Fade = null;
			playback.Current = destination;
		}
		if ( RenderControllerPose() ) return true;

		playback.Current = current;
		playback.Fade = previous;
		for ( var i = 0; i < synchronized.Length; i++ ) synchronized[i].SynchronizedSnapshot = previousSnapshots[i];
		return false;
	}
}

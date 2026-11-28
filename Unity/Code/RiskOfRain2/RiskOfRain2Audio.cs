namespace UnityMount;

static class RiskOfRain2Audio
{
	// The game's logarithmic attackSpeed RTPC and its linear ±4800-cent curve cancel out.
	internal static float AttackPitch( float speed ) => Math.Clamp( speed, 1f / 16f, 16f );

	public static void Play( string bank, string name, Vector3 position, bool local = false, float pitch = 1f )
	{
		if ( Application.IsDedicatedServer ) return;
		if ( LoadEvent( bank, name ) is { } sound )
			PlayLayer( sound, sound.Media, position, local, pitch );
	}

	public static WwiseEventResource LoadEvent( string bank, string name ) =>
		GameResource.Load<WwiseEventResource>( $"mount://ror2/sounds/wwise/events/{bank}/{name}.wwevent" );

	internal static SoundHandle PlayAttached( string bank, string name, GameObject emitter, float pitch = 1f )
	{
		if ( Application.IsDedicatedServer || LoadEvent( bank, name ) is not { } sound ) return null;
		var handle = PlayLayer( sound, sound.Media, emitter.WorldPosition, pitch: pitch );
		Follow( handle, emitter );
		return handle;
	}

	public static SoundHandle PlayLayer( WwiseEventResource sound, WwiseEventResource.MediaReference[] media, Vector3 position,
		bool local = false, float pitch = 1f, float delay = 0f )
	{
		var samples = media.Where( sample => sample.Path is not null ).ToArray();
		if ( samples is not { Length: > 0 } ) return null;
		if ( SoundFile.Load( Game.Random.FromArray( samples ).Path ) is not { } file ) return null;

		return PlayFile( sound, file, position, local, pitch, delay );
	}

	internal static SoundHandle PlayFile( WwiseEventResource sound, SoundFile file, Vector3 position,
		bool local = false, float pitch = 1f, float delay = 0f, float fadeIn = 0f )
	{
		var handle = Sound.PlayFile( file, pitch: pitch, delay: delay, fadeInTime: fadeIn );
		if ( !handle.IsValid() ) return null;
		handle.Position = position;
		handle.SpacialBlend = local ? 0f : 1f;
		if ( sound.MaxAttenuation is { } distance )
			handle.Distance = UnityCoordinates.ToInches( distance );
		return handle;
	}

	internal static void Follow( SoundHandle handle, GameObject emitter )
	{
		if ( !handle.IsValid() ) return;
		handle.Parent = emitter;
		handle.LocalTransform = Transform.Zero;
		handle.FollowParent = true;
	}
}

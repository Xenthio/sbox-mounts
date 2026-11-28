namespace UnityMount;

public sealed partial class UnityTimelinePlayer
{
	public sealed record AudioCue( GameObject Emitter, double Start, double End, string EventPath, bool StopAtEnd, long? OnceId );

	[Property, Hide] public AudioCue[] AudioCues { get; set; } = [];

	readonly HashSet<long> _startedEmitters = new();
	readonly List<(AudioCue Cue, SoundHandle Handle)> _sounds = new();

	void AdvanceSounds( double previous )
	{
		if ( Application.IsDedicatedServer ) return;
		_sounds.RemoveAll( sound =>
		{
			if ( !sound.Handle.IsValid() || sound.Handle.Finished ) return true;
			if ( !sound.Cue.StopAtEnd || Position < sound.Cue.End ) return false;
			sound.Handle.Stop();
			return true;
		} );

		foreach ( var cue in AudioCues )
		{
			if ( cue.Start <= previous || cue.Start > Position || Position >= cue.End || !cue.Emitter.IsValid() ||
				(cue.OnceId is { } id && _startedEmitters.Contains( id )) ) continue;
			if ( GameResource.Load<WwiseEventResource>( cue.EventPath ) is not { } sound ||
				RiskOfRain2Audio.PlayLayer( sound, sound.Media, cue.Emitter.WorldPosition ) is not { } handle ) continue;

			if ( cue.OnceId is { } onceId ) _startedEmitters.Add( onceId );
			_sounds.Add( (cue, handle) );
		}
	}

	void StopSounds( bool includeTails )
	{
		_sounds.RemoveAll( sound =>
		{
			if ( !sound.Handle.IsValid() || sound.Handle.Finished ) return true;
			if ( !includeTails && !sound.Cue.StopAtEnd ) return false;
			sound.Handle.Stop();
			return true;
		} );
	}
}

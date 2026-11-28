using System.Runtime.CompilerServices;
using UnityMount.Wwise;

namespace UnityMount;

sealed class WwiseSoundLoader( string mediaPath, long offset = 0, int? size = null, bool loop = false ) : ResourceLoader<GameMount>
{
	sealed record SoundMetadata( float Duration );
	static readonly ConditionalWeakTable<SoundFile, SoundMetadata> Metadata = new();

	// Runtime-created SoundFiles can play before their native resource metadata is available.
	internal static float GetDuration( SoundFile file ) =>
		Metadata.TryGetValue( file, out var metadata ) ? metadata.Duration : 0f;

	protected override object Load()
	{
		using var stream = Host.FileSystem.OpenRead( mediaPath );
		using var reader = new UnityReader( stream );
		reader.Position = offset;
		var length = size ?? reader.Remaining;
		if ( length <= 0 || length > WwiseVorbis.MaxEncodedBytes )
			throw new InvalidDataException( "Wwise media exceeds the supported encoded size." );

		var audio = WwiseVorbis.Convert( reader.ReadExactBytes( (int)length ) );
		var file = SoundFile.FromOgg( Path, audio.Data, new SoundFile.LoadOptions
			{ Loop = loop, LoopStart = audio.LoopStart, LoopEnd = audio.LoopEnd } );
		if ( file is not null ) Metadata.GetValue( file, _ => new( audio.Duration ) );
		return file;
	}
}

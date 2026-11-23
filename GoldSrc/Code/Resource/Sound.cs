namespace GoldSrc;

using System;
using System.Buffers.Binary;

class WavSoundLoader( string fullPath ) : ResourceLoader<GameMount>
{
	protected override object Load()
	{
		var data = Host.FileSystem.ReadAllBytes( fullPath ).ToArray();
		// Sven Co-op keeps the legacy WAV names for some replacement Vorbis samples.
		if ( data.AsSpan().StartsWith( "OggS"u8 ) ) return SoundFile.FromOgg( Path, data );
		FixRiffPadding( data );
		return SoundFile.FromWav( Path, data );
	}

	static void FixRiffPadding( Span<byte> data )
	{
		if ( data.Length < 20 || !data.StartsWith( "RIFF"u8 ) || !data[8..].StartsWith( "WAVE"u8 ) ) return;
		var declaredEnd = (long)BinaryPrimitives.ReadUInt32LittleEndian( data[4..] ) + 8;
		if ( declaredEnd != data.Length - 1 || data[^1] != 0 ) return;

		long offset = 12;
		while ( offset + 8 <= declaredEnd )
		{
			var size = BinaryPrimitives.ReadUInt32LittleEndian( data[((int)offset + 4)..] );
			var end = offset + 8 + size;
			// Some GoldSrc samples include the last chunk's pad byte but omit it from the RIFF size.
			if ( end == declaredEnd && (size & 1) != 0 )
				BinaryPrimitives.WriteUInt32LittleEndian( data[4..], (uint)(data.Length - 8) );
			offset = end + (size & 1);
		}
	}
}

internal static class GoldSrcSoundEvents
{
	private static readonly Curve LinearFalloff = new(
		new Curve.Frame( 0f, 1f ) { Mode = Curve.HandleMode.Linear },
		new Curve.Frame( 1f, 0f ) );

	internal const float Normal = 0.8f;
	internal const float Idle = 2f;

	static float Range( float attenuation ) => attenuation > 0 ? 1000f / attenuation : float.MaxValue;

	internal static SoundHandle Play( SoundFile sound, Vector3 position, float volume = 1, float pitch = 1, float attenuation = Normal )
	{
		if ( sound is null ) return null;
		var handle = Sound.PlayFile( sound, volume, pitch );
		if ( handle.IsValid() )
		{
			handle.Position = position;
			handle.DistanceAttenuation = attenuation > 0;
			handle.Distance = Range( attenuation );
			handle.Falloff = LinearFalloff;
			handle.OcclusionEnabled = false;
			handle.AirAbsorption = false;
		}
		return handle;
	}

	internal static SoundEvent Create( SoundFile[] sounds, float volume, float attenuation )
	{
		var attenuated = attenuation > 0f;
		return new SoundEvent
		{
			Sounds = [.. sounds],
			SelectionMode = SoundEvent.SoundSelectionMode.Random,
			Volume = volume,
			DistanceAttenuation = attenuated,
			Distance = Range( attenuation ),
			FalloffMode = SoundFalloffMode.Curve,
			Falloff = LinearFalloff,
			OcclusionEnabled = false,
			AirAbsorption = false
		};
	}
}

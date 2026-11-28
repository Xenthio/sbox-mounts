using System.Runtime.CompilerServices;

namespace UnityMount;

/// <summary>Plays authored material curves on individual mounted renderers.</summary>
[Title( "Mounted Material Animation" )]
public sealed class UnityMaterialAnimation : Component, Component.DontExecuteOnServer
{
	public readonly record struct Sample( Vector4 PrintSlice, Vector4 SliceTransform, Vector4 EmissionTint, float PrintBoost,
		float EmissionPower, float FresnelBoost, float ExternalAlpha );
	public sealed record Track( Renderer Renderer, uint Channels, Sample[] Frames );
	public sealed record Clip( string Name, float Duration, float SampleRate, bool Looping, Track[] Tracks );

	const string ChannelMask = "UnityAnimatedMaterialChannels";
	static readonly ConditionalWeakTable<Renderer, Dictionary<UnityMaterialAnimation, (uint Channels, Sample Value)>> ActiveTracks = new();
	[Property, Hide] public Clip[] Clips { get; set; } = [];
	public string CurrentClip => _clip?.Name;
	public float Position { get; private set; }
	public bool Playing { get; private set; }

	Clip _clip;

	public bool Play( string name )
	{
		var clip = Clips.FirstOrDefault( clip => clip.Name == name );
		if ( clip is null ) return false;
		Stop();
		_clip = clip;
		Playing = clip.Duration > 0;
		Seek( 0 );
		return true;
	}

	internal void SampleAt( Clip clip, float seconds )
	{
		if ( _clip != clip )
		{
			Stop();
			_clip = clip;
		}
		Playing = false;
		Seek( clip.Looping && clip.Duration > 0 ? seconds % clip.Duration : seconds );
	}

	public void Seek( float seconds )
	{
		if ( _clip is null || !float.IsFinite( seconds ) ) return;
		Position = Math.Clamp( seconds, 0, _clip.Duration );
		var sample = Position * _clip.SampleRate;
		foreach ( var track in _clip.Tracks )
		{
			if ( !track.Renderer.IsValid() || track.Frames.Length == 0 ) continue;
			var first = Math.Min( (int)sample, track.Frames.Length - 1 );
			var next = Math.Min( first + 1, track.Frames.Length - 1 );
			var start = first / _clip.SampleRate;
			var end = Math.Min( next / _clip.SampleRate, _clip.Duration );
			var amount = end > start ? (Position - start) / (end - start) : 0;
			var a = track.Frames[first];
			var b = track.Frames[next];
			var state = ActiveTracks.GetOrCreateValue( track.Renderer );
			state[this] = (track.Channels, new(
				Vector4.Lerp( a.PrintSlice, b.PrintSlice, amount ),
				Vector4.Lerp( a.SliceTransform, b.SliceTransform, amount ),
				Vector4.Lerp( a.EmissionTint, b.EmissionTint, amount ),
				MathX.Lerp( a.PrintBoost, b.PrintBoost, amount ),
				MathX.Lerp( a.EmissionPower, b.EmissionPower, amount ),
				MathX.Lerp( a.FresnelBoost, b.FresnelBoost, amount ),
				MathX.Lerp( a.ExternalAlpha, b.ExternalAlpha, amount ) ));
			Apply( track.Renderer, state.Values );
		}
	}

	public void Stop()
	{
		if ( _clip is null ) return;
		foreach ( var track in _clip.Tracks )
		{
			if ( !track.Renderer.IsValid() || !ActiveTracks.TryGetValue( track.Renderer, out var state ) ) continue;
			state.Remove( this );
			Apply( track.Renderer, state.Values );
		}
		_clip = null;
		Position = 0;
		Playing = false;
	}

	protected override void OnDisabled() => Stop();

	static void Apply( Renderer renderer, IEnumerable<(uint Channels, Sample Value)> values )
	{
		// The parent animator can tint an eye while a local animator drives its print reveal.
		uint mask = 0;
		var slice = Vector4.Zero;
		var uv = Vector4.Zero;
		var tint = Vector4.Zero;
		var boost = 0f;
		var emissionPower = 0f;
		var fresnelBoost = 0f;
		var alpha = 0f;
		foreach ( var (channels, sample) in values )
		{
			mask |= channels;
			Overlay( ref slice, sample.PrintSlice, channels );
			Overlay( ref uv, sample.SliceTransform, channels >> 4 );
			Overlay( ref tint, sample.EmissionTint, channels >> 8 );
			if ( (channels & (1u << 12)) != 0 ) boost = sample.PrintBoost;
			if ( (channels & (1u << 13)) != 0 ) emissionPower = sample.EmissionPower;
			if ( (channels & (1u << 14)) != 0 ) fresnelBoost = sample.FresnelBoost;
			if ( (channels & (1u << 15)) != 0 ) alpha = sample.ExternalAlpha;
		}
		var attributes = renderer.Attributes;
		attributes.Set( ChannelMask, mask );
		if ( mask == 0 ) return;
		attributes.Set( "UnityAnimatedPrintSlice", slice );
		attributes.Set( "UnityAnimatedSliceTransform", uv );
		attributes.Set( "UnityAnimatedEmissionTint", tint );
		attributes.Set( "UnityAnimatedPrintBoost", boost );
		attributes.Set( "UnityAnimatedEmissionPower", emissionPower );
		attributes.Set( "UnityAnimatedFresnelBoost", fresnelBoost );
		attributes.Set( "UnityAnimatedExternalAlpha", alpha );

		static void Overlay( ref Vector4 value, Vector4 source, uint channels )
		{
			for ( var i = 0; i < 4; i++ )
				if ( (channels & (1u << i)) != 0 ) value[i] = source[i];
		}
	}

	protected override void OnUpdate()
	{
		if ( !Playing ) return;
		var next = Position + Time.Delta;
		Seek( _clip.Looping ? next % _clip.Duration : next );
		if ( !_clip.Looping && next >= _clip.Duration ) Playing = false;
	}
}

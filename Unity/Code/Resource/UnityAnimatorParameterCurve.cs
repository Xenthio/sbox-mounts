using System.Text.Json.Serialization;

namespace UnityMount;

/// <summary>A mounted clip's authored animation curve for an Animator parameter.</summary>
[Sandbox.ModelEditor.GameData( "unity_animator_parameter_curve", AllowMultiple = true ), Hide]
// KeyValues3 rounds numeric JSON values on export. Keep the source precision for threshold crossings.
[JsonNumberHandling( JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString )]
public sealed record UnityAnimatorParameterCurve
{
	public string Sequence { get; init; }
	public string Parameter { get; init; }
	public float ClipStart { get; init; }
	public float Duration { get; init; }
	public Key[] Keys { get; init; } = [];
	public float DenseStart { get; init; }
	public float DenseRate { get; init; }
	public float[] Samples { get; init; } = [];

	[JsonNumberHandling( JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString )]
	public readonly record struct Key( float Time, float Cubic, float Quadratic, float Linear, float Value );

	/// <summary>Samples a clip at a time in seconds relative to its authored start.</summary>
	public float Sample( float seconds )
	{
		if ( !float.IsFinite( seconds ) ) return 0;
		var time = ClipStart + Math.Clamp( seconds, 0, Duration );
		if ( Keys.Length > 0 ) return SampleStreamed( Keys, time );
		if ( Samples.Length == 0 ) return 0;
		if ( Samples.Length == 1 ) return Samples[0];
		var sample = Math.Clamp( (time - DenseStart) * DenseRate, 0, Samples.Length - 1 );
		var first = (int)sample;
		var next = Math.Min( first + 1, Samples.Length - 1 );
		return Samples[first] + (Samples[next] - Samples[first]) * (sample - first);
	}

	internal static float SampleStreamed( Key[] keys, float time )
	{
		if ( keys.Length == 0 ) return 0;
		var lo = 0;
		var hi = keys.Length;
		while ( lo + 1 < hi )
		{
			var mid = (lo + hi) / 2;
			if ( keys[mid].Time <= time ) lo = mid;
			else hi = mid;
		}

		var key = keys[lo];
		var delta = Math.Max( 0, time - key.Time );
		var value = ((key.Cubic * delta + key.Quadratic) * delta + key.Linear) * delta + key.Value;
		return float.IsFinite( value ) ? value : key.Value;
	}
}

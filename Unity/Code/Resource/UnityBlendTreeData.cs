using System.Text.Json.Serialization;

namespace UnityMount;

/// <summary>An authored Animator blend tree bound to sequences on a mounted model.</summary>
[Sandbox.ModelEditor.GameData( "unity_blend_tree", AllowMultiple = true ), Hide]
public sealed record UnityBlendTreeData
{
	public enum BlendType { Simple1D, SimpleDirectional2D, FreeformDirectional2D, FreeformCartesian2D, Direct }

	public string State { get; init; }
	public string StatePath { get; init; }
	public int TreeIndex { get; init; }
	public float Speed { get; init; }
	public string SpeedParameter { get; init; }
	public float CycleOffset { get; init; }
	public bool Looping { get; init; }
	public bool Mirrored { get; init; }
	public Node[] Nodes { get; init; } = [];

	public sealed record Node
	{
		public BlendType Type { get; init; }
		public string ParameterX { get; init; }
		public string ParameterY { get; init; }
		public int[] Children { get; init; } = [];
		public float[] Thresholds { get; init; } = [];
		public Vector2[] Positions { get; init; } = [];
		public float[] Magnitudes { get; init; } = [];
		// Unity can store undefined coefficients for a sample paired with itself.
		public Vector2?[] PairVectors { get; init; } = [];
		public float?[] PairAverageMagnitudeInverses { get; init; } = [];
		public int[][] Neighbors { get; init; } = [];
		public string[] DirectParameters { get; init; } = [];
		public bool NormalizeDirectWeights { get; init; }
		public int ClipSlot { get; init; } = -1;
		public string Sequence { get; init; }
		public UnboundMotion UnboundClip { get; init; }
		public float SourceCycleOffset { get; init; }
		public bool SourceLoops { get; init; }
		public float Duration { get; init; }
		public float CycleOffset { get; init; }
		public bool Mirrored { get; init; }
	}

	/// <summary>A decoded clip with a clock but no matching bone tracks on this model.</summary>
	public sealed record UnboundMotion( string Name,
		[property: JsonNumberHandling( JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString )] float Duration );

	internal readonly record struct SequenceInfo( string Name, float CycleOffset, bool Looping, UnboundMotion UnboundClip = null );

	internal UnityBlendTreeData Bind( IReadOnlyList<SequenceInfo?> sequences )
	{
		var nodes = Nodes.ToArray();
		for ( var i = 0; i < nodes.Length; i++ )
		{
			var node = nodes[i];
			if ( node.Children.Length > 0 ) continue;
			var slot = node.ClipSlot;
			if ( (uint)slot >= sequences.Count || sequences[slot] is not { } sequence ) return null;
			nodes[i] = node with { Sequence = sequence.Name, UnboundClip = sequence.UnboundClip,
				SourceCycleOffset = sequence.CycleOffset, SourceLoops = sequence.Looping };
		}
		return this with { Nodes = nodes };
	}
}

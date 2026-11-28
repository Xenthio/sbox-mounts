namespace UnityMount;

/// <summary>The authored ground or air navigation graph referenced by a Risk of Rain 2 scene.</summary>
[AssetType( Name = "Risk of Rain 2 Node Graph", Extension = "ror2nav" )]
public sealed class RiskOfRain2NodeGraph : GameResource
{
	public readonly record struct Node( Vector3 Position, int FirstLink, uint LinkCount, int ForbiddenHulls, byte GateIndex, byte Flags );
	public readonly record struct Link( int From, int To, float DistanceScore, float MaxSlope, float MinJumpHeight,
		int HullMask, int JumpHullMask, byte GateIndex );

	public string GraphName { get; set; }
	public Vector3 CellSize { get; set; }
	public Node[] Nodes { get; set; } = [];
	/// <summary>Links retain their authored order and distance scores. Jump heights use s&amp;box units.</summary>
	public Link[] Links { get; set; } = [];
	public string[] GateNames { get; set; } = [];
}

sealed class RiskOfRain2NodeGraphLoader( UnityAssetFile file, SerializedFile.Object asset ) : ResourceLoader<GameMount>
{
	protected override object Load() => RiskOfRain2NodeGraphs.Read( file, asset );
}

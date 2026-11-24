using GoldSrc.Maps;

namespace GoldSrc.ConditionZeroDeletedScenes;

[Title( "Deleted Scenes Hostage Rescue Point" )]
public sealed class HostageRescuePoint : MapEntity
{
	const float Radius = 256;

	internal bool Contains( Vector3 position ) => CanTrigger && WorldPosition.DistanceSquared( position ) < Radius * Radius;
}

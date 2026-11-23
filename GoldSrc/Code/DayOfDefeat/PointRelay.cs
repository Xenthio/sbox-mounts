using GoldSrc.Maps;

namespace GoldSrc.DayOfDefeat;

[Title( "Day of Defeat Point Relay" ), Category( "Day of Defeat" )]
public sealed class PointRelay : MapEntity
{
	[Property] public ObjectiveTeam Team { get; set; }
	[Property] public ControlPoint Point { get; set; }

	public override void Trigger( GameObject activator = null )
	{
		if ( CanTrigger && Point.IsValid() ) Point.Capture( Team, activator );
	}

	public override void TurnOff( GameObject activator = null ) => Trigger( activator );
}

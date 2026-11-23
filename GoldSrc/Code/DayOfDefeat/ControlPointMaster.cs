using GoldSrc.Maps;

namespace GoldSrc.DayOfDefeat;

[Title( "Day of Defeat Control Point Master" ), Category( "Day of Defeat" )]
public sealed class ControlPointMaster : MapEntity
{
	[Property] public string Group { get; set; }
	[Property] public Doo OnAlliesCapture { get; set; }
	[Property] public Doo OnAxisCapture { get; set; }
	[Property, Sync( SyncFlags.FromHost )] public int AlliesScore { get; private set; }
	[Property, Sync( SyncFlags.FromHost )] public int AxisScore { get; private set; }
	[Property, Sync( SyncFlags.FromHost )] public ObjectiveTeam Winner { get; private set; }

	internal void Captured( ObjectiveTeam team, int score, GameObject activator )
	{
		if ( !CanTrigger || team == ObjectiveTeam.Neutral || Winner != ObjectiveTeam.Neutral ) return;
		if ( team == ObjectiveTeam.Allies ) AlliesScore += score;
		else AxisScore += score;

		var points = Map.Entities.OfType<ControlPoint>().Where( point => point.IsValid() && point.Group == Group ).ToArray();
		if ( points.Length == 0 || points.Any( point => point.Owner != team ) ) return;
		Winner = team;
		Fire( Winner == ObjectiveTeam.Allies ? OnAlliesCapture : OnAxisCapture, activator );
	}
}

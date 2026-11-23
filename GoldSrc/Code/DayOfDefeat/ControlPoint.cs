using GoldSrc.Maps;

namespace GoldSrc.DayOfDefeat;

public enum ObjectiveTeam
{
	Neutral,
	Allies,
	Axis
}

[Title( "Day of Defeat Control Point" ), Category( "Day of Defeat" )]
public sealed class ControlPoint : MapEntity
{
	public sealed class TeamState
	{
		public Model Model { get; set; }
		public int BodyGroup { get; set; }
		public SoundEvent CaptureSound { get; set; }
		public Doo Output { get; set; }
	}

	[Property] public string Group { get; set; }
	[Property] public int TeamPoints { get; set; }
	[Property] public TeamState Neutral { get; set; }
	[Property] public TeamState Allies { get; set; }
	[Property] public TeamState Axis { get; set; }
	[Property] public SkinnedModelRenderer Renderer { get; set; }
	[Property, Sync( SyncFlags.FromHost ), Change( nameof( OnOwnerChanged ) )]
	public ObjectiveTeam Owner { get; set; }

	internal void Capture( ObjectiveTeam team, GameObject activator )
	{
		if ( !Networking.IsHost || !CanTrigger || !IsMasterTriggered( activator ) ) return;
		Owner = team;
		var state = State( team );
		if ( state?.CaptureSound is { } sound ) Sound.Play( sound, WorldPosition );
		Fire( state?.Output, activator );
		var masters = Map.Entities.OfType<ControlPointMaster>().Where( master => master.Group == Group ).ToArray();
		foreach ( var master in masters )
		{
			if ( master.IsValid() ) master.Captured( team, TeamPoints, activator );
		}
	}

	protected override void OnStart() => UpdateModel();

	void OnOwnerChanged( ObjectiveTeam _, ObjectiveTeam team ) => UpdateModel();

	TeamState State( ObjectiveTeam team ) => team switch
	{
		ObjectiveTeam.Allies => Allies,
		ObjectiveTeam.Axis => Axis,
		_ => Neutral
	};

	void UpdateModel()
	{
		if ( !Renderer.IsValid() ) return;
		var state = State( Owner );
		Renderer.Model = state?.Model;
		Renderer.Enabled = Renderer.Model.IsValid();
		if ( Renderer.Enabled && Renderer.Model.Parts.All.Count > 0 )
			Renderer.SetBodyGroup( 0, state.BodyGroup );
	}
}

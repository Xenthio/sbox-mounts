namespace GoldSrc.TeamFortressClassic;

public enum FortressClass
{
	Unassigned = 0,
	Scout = 1,
	Sniper = 2,
	Soldier = 3,
	Demoman = 4,
	Medic = 5,
	HeavyWeapons = 6,
	Pyro = 7,
	Spy = 8,
	Engineer = 9,
	Civilian = 11
}

/// <summary>
/// Optional Team Fortress Classic team and class assignment for a player in a game or scene.
/// </summary>
[Title( "TFC Player" ), Category( "Team Fortress Classic" )]
public sealed class FortressPlayer : Component
{
	/// <summary>GoldSrc team number (1 to 4). Zero leaves the player unassigned.</summary>
	[Property, Sync( SyncFlags.FromHost )] public int Team { get; set; }
	[Property, Sync( SyncFlags.FromHost )] public FortressClass Class { get; set; }

	internal static FortressClass ClassOf( PlayerController player ) =>
		player?.GetComponent<FortressPlayer>()?.Class ?? FortressClass.Unassigned;

	/// <summary>Returns null when either player has no assigned team.</summary>
	internal static bool? AreTeammates( PlayerController first, PlayerController second )
	{
		var firstTeam = first?.GetComponent<FortressPlayer>()?.Team;
		var secondTeam = second?.GetComponent<FortressPlayer>()?.Team;
		return firstTeam is >= 1 and <= 4 && secondTeam is >= 1 and <= 4 ? firstTeam == secondTeam : null;
	}
}

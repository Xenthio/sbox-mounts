namespace GoldSrc.TeamFortressClassic;

[Hide]
public sealed class Tranquilized : Component
{
	const float Duration = 15;

	[Property] public PlayerController Player { get; set; }
	[Sync( SyncFlags.FromHost )] internal TimeUntil Expires { get; set; }

	bool _applied;

	internal static void Apply( PlayerController player )
	{
		if ( player.GameObject.GetComponentInChildren<Tranquilized>() is { } existing )
		{
			existing.Expires = Duration;
			return;
		}

		var effect = new GameObject( player.GameObject, false, "Tranquilized" );
		var status = effect.AddComponent<Tranquilized>();
		status.Player = player;
		status.Expires = Duration;
		effect.NetworkSpawn( true, null );
	}

	protected override void OnUpdate()
	{
		if ( !Player.IsValid() || !Player.Enabled || Expires <= 0 )
		{
			if ( Networking.IsHost ) GameObject.Destroy();
			return;
		}
		if ( (!Networking.IsHost && Player.IsProxy) || _applied ) return;
		FortressSpeed.SetTranquilized( Player, true );
		_applied = true;
	}

	protected override void OnDisabled()
	{
		if ( _applied && Player.IsValid() ) FortressSpeed.SetTranquilized( Player, false );
		_applied = false;
	}
}

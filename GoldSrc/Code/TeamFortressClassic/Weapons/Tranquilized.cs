namespace GoldSrc.TeamFortressClassic;

[Hide]
public sealed class Tranquilized : Component
{
	const float Duration = 15;
	const float SpeedScale = 0.5f;

	[Property] public PlayerController Player { get; set; }
	[Sync( SyncFlags.FromHost )] internal TimeUntil Expires { get; set; }

	(float Walk, float Run, float Ducked)? _normalSpeeds;

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
		if ( !Networking.IsHost && Player.IsProxy || _normalSpeeds.HasValue ) return;

		_normalSpeeds = (Player.WalkSpeed, Player.RunSpeed, Player.DuckedSpeed);
		Player.WalkSpeed *= SpeedScale;
		Player.RunSpeed *= SpeedScale;
		Player.DuckedSpeed *= SpeedScale;
	}

	protected override void OnDisabled()
	{
		if ( Player.IsValid() && _normalSpeeds is { } speeds )
		{
			if ( Player.WalkSpeed == speeds.Walk * SpeedScale ) Player.WalkSpeed = speeds.Walk;
			if ( Player.RunSpeed == speeds.Run * SpeedScale ) Player.RunSpeed = speeds.Run;
			if ( Player.DuckedSpeed == speeds.Ducked * SpeedScale ) Player.DuckedSpeed = speeds.Ducked;
		}
		_normalSpeeds = null;
	}
}

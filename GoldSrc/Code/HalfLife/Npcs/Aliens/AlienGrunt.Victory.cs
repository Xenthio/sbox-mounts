using Sandbox.Npcs.Layers;

namespace GoldSrc.HalfLife;

public partial class AlienGrunt
{
	const int Crouch = 17, Stand = 19, ThreatDisplay = 57, VictoryDance = 65;
	static readonly int[] VictoryActivities = [Crouch, VictoryDance, VictoryDance, Stand, ThreatDisplay,
		Crouch, VictoryDance, VictoryDance, VictoryDance, VictoryDance, VictoryDance, Stand];

	Vector3? _corpsePosition;
	TimeUntil _approachAt;
	bool _startedApproach;
	int _victoryStep;

	void Component.IDamageable.IEvents.OnDied( Component.IDamageable.DeathEvent death )
	{
		if ( !Networking.IsHost || death.Victim != LastHostile || Health <= 0 ) return;
		_corpsePosition = death.Victim.WorldPosition;
		_approachAt = 0.2f;
	}

	protected override void OnDamaged( Component.IDamageable.DamageEvent damage )
	{
		StopVictory();
		base.OnDamaged( damage );
	}

	void StopVictory()
	{
		if ( !_corpsePosition.HasValue ) return;
		if ( _victoryStep > 0 ) InterruptAction();
		if ( _startedApproach ) Movement?.Stop();
		_corpsePosition = null;
		_startedApproach = false;
		_victoryStep = 0;
	}

	bool UpdateVictory()
	{
		if ( _corpsePosition is not { } corpse ) return false;
		if ( FindVisibleEnemy().IsValid() || PhysicsCarrier.IsValid() )
		{
			StopVictory();
			return false;
		}
		if ( !_startedApproach )
		{
			if ( _approachAt > 0 ) return true;
			InterruptAction();
			_startedApproach = true;
			if ( !MoveToward( corpse - WorldRotation.Forward * 50, 8, 0.6f ) ) return DisplayThreat( corpse );
			return true;
		}
		if ( _victoryStep == 0 )
		{
			if ( Movement.Status == MovementLayer.MovementStatus.Moving ) return true;
			if ( Movement.Status != MovementLayer.MovementStatus.Arrived ) return DisplayThreat( corpse );
			FaceCorpse( corpse );
		}
		if ( AdvanceAction() ) return true;
		if ( _victoryStep < VictoryActivities.Length && WeightedActivity( VictoryActivities[_victoryStep++] ) is { } clip )
		{
			PlayAction( clip );
			return true;
		}
		StopVictory();
		return false;
	}

	bool DisplayThreat( Vector3 corpse )
	{
		FaceCorpse( corpse );
		_victoryStep = VictoryActivities.Length;
		if ( WeightedActivity( ThreatDisplay ) is { } threat ) PlayAction( threat );
		return true;
	}
}

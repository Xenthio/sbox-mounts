namespace GoldSrc.HalfLife;

public partial class AlienGrunt
{
	[Property] public bool Gagged { get; set; }

	static readonly int[] Alerts = [1, 3, 4, 5];
	static readonly int[] Deaths = [1, 4, 5];
	TimeUntil _nextWord, _nextPain;
	int _lastWord;

	void PauseSpeech() => _nextWord = 10 + Game.Random.Int( 0, 10 );

	protected override void Think()
	{
		if ( !UpdateVictory() ) base.Think();
		if ( _nextWord > 0 || !Renderer.IsValid() || !Renderer.Animation.IsValid ) return;
		if ( Gagged && !InCombat )
		{
			_nextWord = 3;
			return;
		}
		_lastWord = (_lastWord + Game.Random.Int( 1, 3 )) % 4;
		EmitSound( $"{Sounds.Idle}{_lastWord + 1}.wav", WorldPosition, speaker: this );
		if ( Game.Random.Int( 1, 10 ) == 1 ) PauseSpeech();
		else _nextWord = Game.Random.Float( 0.5f, 1 );
	}

	internal override void IdleMovement()
	{
		if ( Movement?.MoveTarget.HasValue == true ) Movement.Stop();
	}

	void CombatVoice( string sample )
	{
		PauseSpeech();
		EmitSound( sample, WorldPosition, speaker: this );
	}

	internal override void OnEnemySpotted( GameObject target ) =>
		CombatVoice( $"{Sounds.Alert}{Game.Random.FromArray( Alerts )}.wav" );

	internal override void PainSound()
	{
		if ( _nextPain > 0 ) return;
		_nextPain = 0.6f;
		CombatVoice( Sounds.Pain );
	}

	internal override void DeathSound() => CombatVoice( $"{Sounds.Death}{Game.Random.FromArray( Deaths )}.wav" );
}

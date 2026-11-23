using GoldSrc.HalfLife;

namespace GoldSrc.CryOfFear;

[Title( "Taller" ), Category( "Cry of Fear" )]
[NpcDefinition( "monster_taller", "taller", MonsterClass.AlienMonster, 300, 8, 168, 83.81f,
	Mount = "cof", Attack = NpcAttack.Melee, Damage = 30, Range = 64, Blood = Npc.BloodColor.Red )]
public sealed class Taller : CryOfFearMeleeNpc
{
	const float StrikeReach = 90;
	const int PunchEvent = 1, StompEvent = 2, StepEvent = 3;

	internal override NpcSounds Sounds { get; } = new( Alert: "taller/taller_alert.wav", Pain: "taller/taller_pain.wav" );
	internal override string SwingSounds => "taller/taller_swing.wav";
	internal override string HitSounds => "taller/taller_player_punch.wav";
	internal override string WallSounds => "taller/taller_wall_punch.wav";
	internal override IEnumerable<StudioSequence> ExtraSequences => [ ..base.ExtraSequences, Find( "stamp" ) ];

	internal override void OnModelLoaded( ModelLoader loader ) => EyeHeight = 28;

	internal override StudioSequence SelectMeleeSequence( GameObject target ) =>
		target.GetComponent<PlayerController>() is { IsDucking: true } ? Find( "stamp" ) : base.SelectMeleeSequence( target );

	internal override void PlaySequenceSound( NpcAudio audio, int id )
	{
		if ( id == StepEvent ) NpcAudio.Play( audio.Find( "taller/taller_step.wav" ), WorldPosition );
	}

	internal override void PainSound()
	{
		if ( Game.Random.Int( 0, 5 ) < 2 )
			EmitSound( Sounds.Pain, WorldPosition, pitch: Game.Random.Int( 95, 104 ) / 100f, speaker: this );
	}

	internal override void DeathSound() => EmitSound( "taller/taller_die.wav", WorldPosition, pitch: StrikePitch() );

	internal override void HandleActionEvent( int id )
	{
		if ( id == PunchEvent ) Punch();
		if ( id == StompEvent ) Stomp();
	}

	void Punch()
	{
		var victim = TraceMelee( StrikeReach, Definition.Damage, "club", out _ );
		if ( victim?.GameObject.GetComponent<PlayerController>() is { } player )
			PunchPlayer( player, WorldRotation.Forward * 250 + WorldRotation.Right * 200, new Angles( 5, 0, 0 ) );

		var sound = !victim.IsValid() ? SwingSounds
			: ClassOf( victim.GameObject ) is MonsterClass.None or MonsterClass.Machine ? WallSounds : HitSounds;
		EmitSound( sound, WorldPosition, pitch: StrikePitch() );
		if ( !victim.IsValid() && Game.Random.Int( 0, 1 ) == 1 )
			EmitSound( Sounds.Pain, WorldPosition, pitch: StrikePitch(), speaker: this );
	}

	void Stomp()
	{
		var origin = WorldPosition + Vector3.Up * (Definition.Height * 0.25f);
		var half = new Vector3( 16, 16, 18 );
		var trace = Scene.Trace.Box( new BBox( -half, half ), origin, origin + WorldRotation.Forward * StrikeReach )
			.IgnoreGameObjectHierarchy( GameObject ).UseHitboxes().Run();
		var player = trace.GameObject?.GetComponentInParent<PlayerController>();
		if ( player?.GameObject.GetComponent<IDamageable>() is not { Health: > 0 } victim )
		{
			if ( trace.Hit || trace.StartedSolid ) EmitSound( WallSounds, WorldPosition, pitch: StrikePitch() );
			return;
		}

		var damage = new DamageInfo
		{
			Damage = victim.Health + victim.Armour,
			Origin = origin,
			Position = trace.EndPosition,
			Attacker = GameObject,
			Weapon = GameObject
		};
		damage.Tags.Add( "club" );
		damage.Tags.Add( "alwaysgib" );
		victim.OnDamage( damage );
		EmitSound( "taller/taller_stamp.wav", WorldPosition, pitch: StrikePitch() );
	}

	static float StrikePitch() => Game.Random.Int( 95, 105 ) / 100f;
}

using GoldSrc.HalfLife;

namespace GoldSrc.CryOfFear;

[Title( "Sawcrazy" ), Category( "Cry of Fear" )]
[NpcDefinition( "monster_sawcrazy", "sawcrazy", MonsterClass.AlienMonster, 150, 16, 72, 172.09f,
	Mount = "cof", Attack = NpcAttack.Melee, Damage = 200, Range = 55, Blood = Npc.BloodColor.Red )]
public sealed class Sawcrazy : CryOfFearMeleeNpc
{
	const string MotorSample = "sawcrazy/dblsawloop.wav";

	TimeUntil _nextCut, _nextMotor;
	SoundFile _motorSample;
	SoundHandle _motor;

	internal override NpcSounds Sounds { get; } = new( Death: "sawcrazy/death.wav" );
	internal override void OnModelLoaded( ModelLoader loader ) => EyeHeight = 28;
	internal override bool InAttackRange( GameObject target, Vector3 aim ) => false;
	internal override bool Pursue( Vector3 goal ) => MoveToward( goal, 8 );
	internal override void HandleActionEvent( int id ) { }

	internal override void ModifyDamage( Component.IDamageable.DamageEvent damage )
	{
		base.ModifyDamage( damage );
		if ( damage.DamageInfo.Tags.Has( "bullet" ) ) damage.AcceptedDamage *= 0.3f;
	}

	protected override void Think()
	{
		// The saw sweeps during the normal monster think, independently of its animation.
		if ( _nextCut <= 0 && Find( "idle" ) is not null && Health > 0 )
		{
			_nextCut = 0.1f;
			var victim = TraceMelee( AttackRange, Definition.Damage, "slash", out _ );
			if ( victim is IDamageable { Health: > 0 } && victim.GameObject.GetComponent<PlayerController>() is { } player )
			{
				PunchPlayer( player, -WorldRotation.Right * 100, new Angles( 5, 0, -18 ) );
				EmitSound( "sawrunner/chainsaw_attack_hit.wav", WorldPosition );
			}
		}

		base.Think();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( Health <= 0 || Application.IsDedicatedServer ) return;
		if ( _motorSample is null && Sandbox.Mounting.Directory.Get( "cof" ) is GameMount mount && mount.NpcAudio.Find( MotorSample ) is { } path )
			_motorSample = SoundFile.Load( path );
		if ( _motorSample is null ) return;

		if ( _nextMotor <= 0 )
		{
			_nextMotor = 7;
			_motor?.Stop();
			_motor = GoldSrcSoundEvents.Play( _motorSample, WorldPosition );
		}
		if ( _motor.IsValid() ) _motor.Position = WorldPosition;
	}

	protected override void OnDeath( Component.IDamageable.DeathEvent death )
	{
		_motor?.Stop();
		base.OnDeath( death );
	}

	protected override void OnDisabled()
	{
		_motor?.Stop();
		_nextMotor = 0;
		base.OnDisabled();
	}
}

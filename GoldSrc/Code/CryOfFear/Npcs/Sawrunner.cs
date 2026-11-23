using GoldSrc.HalfLife;

namespace GoldSrc.CryOfFear;

[Title( "Sawrunner" ), Category( "Cry of Fear" )]
[NpcDefinition( "monster_sawrunner", "sawrunner", MonsterClass.AlienMonster, 2000, 16, 72, 365.7f,
	Mount = "cof", Attack = NpcAttack.Melee, Damage = 200, Range = 64, Blood = Npc.BloodColor.Red )]
public sealed class Sawrunner : RushingMeleeNpc
{
	const string SawLoop = "boss/sawer/chainsaw_loop.wav";

	[Sync( SyncFlags.FromHost )] bool SawRunning { get; set; }

	SoundHandle _saw;
	SoundFile _loop;
	TimeUntil _nextFlinch;

	internal override string ChaseSequence => "walk";
	internal override string StrikeVoice => "sawrunner/sawrunner_attack";
	internal override string StrikeDamageTag => "nevergib";

	// The shipped attack selects the miss sample on contact and the hit sample on a miss.
	internal override string HitSounds => "sawrunner/chainsaw_attack_miss.wav";
	internal override string SwingSounds => "sawrunner/chainsaw_attack_hit.wav";
	internal override NpcSounds Sounds { get; } = new( Alert: "sawrunner/sawrunner_alert", Pain: "sawrunner/sawrunner_pain" );

	internal override void OnModelLoaded( ModelLoader loader ) => EyeHeight = 28;

	internal override void OnEnemySpotted( GameObject target )
	{
		SawRunning = true;
		EmitSound( Sounds.Alert, WorldPosition, pitch: Game.Random.Int( 95, 104 ) / 100f );
	}

	internal override void PainSound()
	{
		if ( Game.Random.Int( 0, 5 ) < 2 )
			EmitSound( Sounds.Pain, WorldPosition, pitch: Game.Random.Int( 95, 104 ) / 100f );
	}

	internal override StudioSequence SelectFlinch( Component.IDamageable.DamageEvent damage )
	{
		if ( _nextFlinch > 0 || base.SelectFlinch( damage ) is not { } flinch ) return null;
		_nextFlinch = 2;
		return flinch;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( !SawRunning || Health <= 0 || Application.IsDedicatedServer ) return;
		if ( _loop is null && Sandbox.Mounting.Directory.Get( "cof" ) is GameMount mount && mount.NpcAudio.Find( SawLoop ) is { } path )
			_loop = SoundFile.Load( path );
		if ( _loop is null ) return;

		if ( !_saw.IsValid() || !_saw.IsPlaying )
			_saw = GoldSrcSoundEvents.Play( _loop, WorldPosition );
		if ( _saw.IsValid() ) _saw.Position = WorldPosition;
	}

	protected override void OnDeath( Component.IDamageable.DeathEvent death )
	{
		SawRunning = false;
		_saw?.Stop();
		base.OnDeath( death );
	}

	protected override void OnDisabled()
	{
		_saw?.Stop();
		base.OnDisabled();
	}
}

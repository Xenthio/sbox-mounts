using GoldSrc.HalfLife;

namespace GoldSrc.CryOfFear;

[Hide]
public abstract class TwitcherNpc : RushingMeleeNpc
{
	[Sync( SyncFlags.FromHost )] string Gait { get; set; }
	TimeUntil _nextFlinch;
	NpcSounds _sounds;

	string SoundFolder => Definition.Model[(Definition.Model.LastIndexOf( '/' ) + 1)..];
	internal override string ChaseSequence => Gait ?? "walk";
	internal override string StrikeVoice => $"{SoundFolder}/zo_attack";
	internal override string HitSounds => $"{SoundFolder}/claw_strike";
	internal override string SwingSounds => $"{SoundFolder}/claw_miss";
	internal override NpcSounds Sounds => _sounds ??= new(
		Idle: $"{SoundFolder}/zo_idle", Alert: $"{SoundFolder}/zo_alert", Pain: $"{SoundFolder}/zo_pain" );

	internal override void OnModelLoaded( ModelLoader loader ) => EyeHeight = 28;
	internal override IEnumerable<StudioSequence> ExtraSequences => base.ExtraSequences.Concat( Activities( Walk ) );
	internal override StudioSequence MovementSequence( float speed ) => speed > 12 ? Find( Gait ) ?? base.MovementSequence( speed ) : null;

	internal override bool Pursue( Vector3 goal )
	{
		Gait ??= WeightedActivity( Walk )?.Name;
		return base.Pursue( goal );
	}

	internal override void BeginAttack( GameObject target )
	{
		Gait = null;
		base.BeginAttack( target );
	}

	internal override void IdleMovement()
	{
		Gait = null;
		base.IdleMovement();
		if ( NextWander > 0 ) return;
		NextWander = Game.Random.Float( 3, 7 );
		IdleSound();
	}

	internal override void OnEnemySpotted( GameObject target ) =>
		EmitSound( Sounds.Alert, WorldPosition, pitch: Game.Random.Int( 95, 104 ) / 100f );

	internal override float VoicePitch => Game.Random.Int( 95, 105 ) / 100f;

	internal override void PainSound()
	{
		if ( Game.Random.Int( 0, 5 ) < 2 )
			EmitSound( Sounds.Pain, WorldPosition, pitch: Game.Random.Int( 95, 104 ) / 100f );
	}

	internal override StudioSequence SelectFlinch( Component.IDamageable.DamageEvent damage )
	{
		if ( _nextFlinch > 0 || base.SelectFlinch( damage ) is not { } flinch ) return null;
		Gait = null;
		_nextFlinch = 2;
		return flinch;
	}
}

[Title( "Twitcher" ), Category( "Cry of Fear" )]
[NpcDefinition( "monster_twitcher", "props/blandat/zombie", MonsterClass.AlienMonster, 40, 16, 72, 268.64f,
	Mount = "cof", Attack = NpcAttack.Melee, Damage = 7, Range = 64, Blood = Npc.BloodColor.Red )]
public sealed class Twitcher : TwitcherNpc;

[Title( "Twitcher 2" ), Category( "Cry of Fear" )]
[NpcDefinition( "monster_twitcher2", "props/blandat/zombie2", MonsterClass.AlienMonster, 40, 16, 72, 295.51f,
	Mount = "cof", Attack = NpcAttack.Melee, Damage = 7, Range = 64, Blood = Npc.BloodColor.Red )]
public sealed class Twitcher2 : TwitcherNpc;

[Title( "Twitcher 3" ), Category( "Cry of Fear" )]
[NpcDefinition( "monster_twitcher3", "props/blandat/zombie3", MonsterClass.AlienMonster, 40, 16, 72, 268.01f,
	Mount = "cof", Attack = NpcAttack.Melee, Damage = 6, Range = 64, Blood = Npc.BloodColor.Red )]
public sealed class Twitcher3 : TwitcherNpc;

[Title( "Twitcher 4" ), Category( "Cry of Fear" )]
[NpcDefinition( "monster_twitcher4", "props/blandat/zombie4", MonsterClass.AlienMonster, 40, 16, 72, 249,
	Mount = "cof", Attack = NpcAttack.Melee, Damage = 6, Range = 64, Blood = Npc.BloodColor.Red )]
public sealed class Twitcher4 : TwitcherNpc;

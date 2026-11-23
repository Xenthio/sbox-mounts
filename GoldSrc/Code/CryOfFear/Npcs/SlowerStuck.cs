using GoldSrc.HalfLife;

namespace GoldSrc.CryOfFear;

[Title( "Slower Stuck" ), Category( "Cry of Fear" )]
[NpcDefinition( "monster_slowerstuck", "tenslower2", MonsterClass.AlienMonster, 40, 12, 72, 387.87f,
	Mount = "cof", Attack = NpcAttack.Melee, Damage = 7, Range = 64, Blood = Npc.BloodColor.Red )]
public sealed class SlowerStuck : RushingMeleeNpc
{
	internal override string ChaseSequence => "walk";
	internal override string StrikeVoice => "slower/slower_attack";
	internal override string WallSounds => "slower/hammer_strike_wall";
	internal override NpcSounds Sounds { get; } = new( Alert: "slower/slower_alert", Pain: "slower/slower_pain" );

	internal override void OnModelLoaded( ModelLoader loader ) => EyeHeight = 28;

	// The default model is rooted in place. Some maps give this actor a mobile model.
	internal override bool Pursue( Vector3 goal ) => Find( ChaseSequence ) is not null && base.Pursue( goal );

	internal override void PainSound()
	{
		if ( Game.Random.Int( 0, 5 ) < 2 )
			EmitSound( Sounds.Pain, WorldPosition, pitch: Game.Random.Int( 95, 104 ) / 100f, speaker: this );
	}
}

using GoldSrc.HalfLife;

namespace GoldSrc.CryOfFear;

[Title( "Croucher" ), Category( "Cry of Fear" )]
[NpcDefinition( "monster_crab", "slower2", MonsterClass.AlienMonster, 30, 4, 36, 234.08f,
	Mount = "cof", Attack = NpcAttack.Melee, Damage = 4, Range = 64, Blood = Npc.BloodColor.Red )]
public sealed class Croucher : RushingMeleeNpc
{
	internal override string ChaseSequence => "walk2";
	internal override string StrikeVoice => "slower/slower_attack";
	internal override string WallSounds => "slower/hammer_strike_wall";
	internal override NpcSounds Sounds { get; } = new( Alert: "slower/slower_alert", Pain: "slower/slower_pain" );

	internal override void OnModelLoaded( ModelLoader loader ) => EyeHeight = 28;

	internal override void PainSound()
	{
		if ( Game.Random.Int( 0, 5 ) < 2 )
			EmitSound( Sounds.Pain, WorldPosition, pitch: Game.Random.Int( 95, 104 ) / 100f, speaker: this );
	}
}

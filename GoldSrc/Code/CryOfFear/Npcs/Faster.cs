using GoldSrc.HalfLife;

namespace GoldSrc.CryOfFear;

[Title( "Faster" ), Category( "Cry of Fear" )]
[NpcDefinition( "monster_faster", "faster", MonsterClass.AlienMonster, 40, 16, 72, 207,
	Mount = "cof", Attack = NpcAttack.Melee, Damage = 5, Range = 64, Blood = Npc.BloodColor.Red )]
public sealed class Faster : RushingMeleeNpc
{
	internal override string ChaseSequence => "run";
	internal override string StrikeVoice => "faster/faster_attack.wav";
	internal override string SwingSounds => "weapons/switchblade/switchblade_hitwall1.wav";
	internal override string HitSounds => "faster/faster_hit";
	internal override NpcSounds Sounds { get; } = new( Alert: "faster/faster_alert", Pain: "faster/faster_pain.wav", Death: "faster/faster_death.wav" );

	internal override void PlaySequenceSound( NpcAudio audio, int id )
	{
		if ( id == 1018 ) NpcAudio.Play( audio.Find( "faster/faster_step.wav" ), WorldPosition );
	}
}

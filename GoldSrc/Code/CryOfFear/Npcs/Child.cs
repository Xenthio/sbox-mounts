using GoldSrc.HalfLife;

namespace GoldSrc.CryOfFear;

[Title( "Child" ), Category( "Cry of Fear" )]
[NpcDefinition( "monster_child", "children", MonsterClass.AlienMonster, 35, 16, 65, 166,
	Mount = "cof", Attack = NpcAttack.Melee, Damage = 3, Range = 64, Blood = Npc.BloodColor.Red )]
public sealed class Child : RushingMeleeNpc
{
	internal override string ChaseSequence => "walk";
	internal override string SwingSounds => "children/child_slash.wav";
	internal override string HitSounds => "children/child_slice.wav";
	internal override string WallSounds => null;
	internal override string StrikeVoice => "children/child_attack";
	internal override NpcSounds Sounds { get; } = new( Alert: "children/child_alert", Pain: "children/child_pain" );
}

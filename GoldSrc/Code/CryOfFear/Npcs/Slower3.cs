using GoldSrc.HalfLife;

namespace GoldSrc.CryOfFear;

[Title( "Slower 3" ), Category( "Cry of Fear" )]
[NpcDefinition( "monster_slower3", "slower3", MonsterClass.AlienMonster, 40, 16, 72, 305,
	Mount = "cof", Attack = NpcAttack.Melee, Damage = 7, Range = 64, Blood = Npc.BloodColor.Red )]
public sealed class Slower3 : CryOfFearMeleeNpc
{
	internal override NpcSounds Sounds { get; } = new( Alert: "slower3/slower_alert", Pain: "slower3/slower_pain", Attack: "slower3/slower_attack" );
}

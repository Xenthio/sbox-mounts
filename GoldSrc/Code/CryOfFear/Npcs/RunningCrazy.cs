using GoldSrc.HalfLife;

namespace GoldSrc.CryOfFear;

[Title( "Running Crazy" ), Category( "Cry of Fear" )]
[NpcDefinition( "monster_rcrazy", "runningcrazy", MonsterClass.AlienMonster, 37, 16, 72, 281,
	Mount = "cof", Attack = NpcAttack.Melee, Damage = 4, Range = 64, Blood = Npc.BloodColor.Red )]
public sealed class RunningCrazy : RushingMeleeNpc
{
	internal override string ChaseSequence => "walk";
	internal override string SwingSounds => "children/child_slash.wav";
	internal override string HitSounds => "children/child_slice.wav";
	internal override NpcSounds Sounds { get; } = new( Alert: "rcrazy/rc_alert" );
}

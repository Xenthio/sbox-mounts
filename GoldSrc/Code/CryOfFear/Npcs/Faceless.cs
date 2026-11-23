using GoldSrc.HalfLife;

namespace GoldSrc.CryOfFear;

[Title( "Faceless" ), Category( "Cry of Fear" )]
[NpcDefinition( "monster_faceless", "faceless", MonsterClass.AlienMonster, 40, 16, 72, 159,
	Mount = "cof", Attack = NpcAttack.Melee, Damage = 5, Range = 64, Blood = Npc.BloodColor.Red )]
public sealed class Faceless : CryOfFearMeleeNpc
{
	internal override NpcSounds Sounds { get; } = new( Alert: "faceless/faceless_alert", Pain: "faceless/faceless_pain", Attack: "faceless/faceless_attack" );
	internal override string SwingSounds => "faceless/fist_miss";
	internal override string HitSounds => "faceless/fist_strike";
	internal override string WallSounds => SwingSounds;
}

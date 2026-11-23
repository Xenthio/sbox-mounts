using GoldSrc.HalfLife;

namespace GoldSrc.CryOfFear;

[Title( "Slower" ), Category( "Cry of Fear" )]
[NpcDefinition( "monster_slower", "slower", MonsterClass.AlienMonster, 40, 16, 72, 96,
	Mount = "cof", Attack = NpcAttack.Melee, Damage = 7, Range = 64, Blood = Npc.BloodColor.Red )]
public sealed class Slower : CryOfFearMeleeNpc
{
	public Slower() => Body = -1;

	// The remaining body choices are the three headless counterparts.
	internal override int EffectiveBody => Body == -1 ? (int)((uint)GameObject.Id.GetHashCode() % 3) : Body;
	internal override NpcSounds Sounds { get; } = new( Alert: "slower/slower_alert", Pain: "slower/slower_pain", Attack: "slower/slower_attack" );
}

using GoldSrc.HalfLife;

namespace GoldSrc.SvenCoop;

[Title( "Baby Gargantua" )]
[NpcDefinition( "monster_babygarg", "babygarg", MonsterClass.AlienMonster, 600, 32, 64, 145,
	Mount = "svencoop", Attack = NpcAttack.Melee, Damage = 25, Range = 40, StrikeEvents = [1], Blood = Npc.BloodColor.Yellow )]
public sealed class BabyGargantua : GargantuaNpc
{
	internal override float AttackScale => 0.5f;
	internal override float FlameDamage => 2;
	internal override float StompDamage => 50;
	internal override bool Armored => false;
	internal override string SoundDirectory => "babygarg";

	internal override void OnModelLoaded( ModelLoader loader )
	{
		base.OnModelLoaded( loader );
		EyeHeight = loader.EyePosition.z;
	}
}

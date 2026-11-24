using GoldSrc.HalfLife;

namespace GoldSrc.ConditionZeroDeletedScenes;

[Title( "Urban Machete Soldier" )]
[NpcDefinition( "monster_terrorist_urban_melee", "militia", MonsterClass.HumanMilitary, 100, 16, 72, 261,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Melee, Range = 80, IdleSequence = "machete_idle1" )]
public sealed class UrbanMacheteSoldier : MacheteSoldier;

[Title( "Arctic Machete Soldier" )]
[NpcDefinition( "monster_terrorist_arctic_melee", "arctic", MonsterClass.HumanMilitary, 100, 16, 72, 261,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Melee, Range = 80, IdleSequence = "machete_idle1" )]
public sealed class ArcticMacheteSoldier : MacheteSoldier;

[Title( "Asian Machete Soldier" )]
[NpcDefinition( "monster_terrorist_asian_melee", "asian", MonsterClass.HumanMilitary, 100, 16, 72, 261,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Melee, Range = 80, IdleSequence = "machete_idle1" )]
public sealed class AsianMacheteSoldier : MacheteSoldier;

[Title( "Desert Machete Soldier" )]
[NpcDefinition( "monster_terrorist_desert_melee", "leet", MonsterClass.HumanMilitary, 100, 16, 72, 261,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Melee, Range = 80, IdleSequence = "machete_idle1" )]
public sealed class DesertMacheteSoldier : MacheteSoldier;

[Title( "Jungle Machete Soldier" )]
[NpcDefinition( "monster_terrorist_jungle_melee", "guerilla", MonsterClass.HumanMilitary, 100, 16, 72, 261,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Melee, Range = 80, IdleSequence = "machete_idle1" )]
public sealed class JungleMacheteSoldier : MacheteSoldier;

[Title( "Russian Machete Soldier" )]
[NpcDefinition( "monster_terrorist_russian_melee", "terror", MonsterClass.HumanMilitary, 100, 16, 72, 261,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Melee, Range = 80, IdleSequence = "machete_idle1" )]
public sealed class RussianMacheteSoldier : MacheteSoldier;

using GoldSrc.HalfLife;

namespace GoldSrc.ConditionZeroDeletedScenes;

[Title( "Urban Bomb Carrier" )]
[NpcDefinition( "monster_terrorist_urban_kamakazi", "militia", MonsterClass.HumanMilitary, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Melee, Gibs = GibSet.Human, Damage = 100, Range = 64,
	IdleSequence = "machete_idle1", Aliases = ["monster_t_urban_kamakazi"] )]
public sealed class UrbanBombCarrier : BombCarrier;

[Title( "Arctic Bomb Carrier" )]
[NpcDefinition( "monster_terrorist_arctic_kamakazi", "arctic", MonsterClass.HumanMilitary, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Melee, Gibs = GibSet.Human, Damage = 100, Range = 64,
	IdleSequence = "machete_idle1", Aliases = ["monster_t_arctic_kamakazi"] )]
public sealed class ArcticBombCarrier : BombCarrier;

[Title( "Asian Bomb Carrier" )]
[NpcDefinition( "monster_terrorist_asian_kamakazi", "asian", MonsterClass.HumanMilitary, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Melee, Gibs = GibSet.Human, Damage = 100, Range = 64,
	IdleSequence = "machete_idle1", Aliases = ["monster_t_asian_kamakazi"] )]
public sealed class AsianBombCarrier : BombCarrier;

[Title( "Desert Bomb Carrier" )]
[NpcDefinition( "monster_terrorist_desert_kamakazi", "leet", MonsterClass.HumanMilitary, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Melee, Gibs = GibSet.Human, Damage = 100, Range = 64,
	IdleSequence = "machete_idle1", Aliases = ["monster_t_desert_kamakazi"] )]
public sealed class DesertBombCarrier : BombCarrier;

[Title( "Jungle Bomb Carrier" )]
[NpcDefinition( "monster_terrorist_jungle_kamakazi", "guerilla", MonsterClass.HumanMilitary, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Melee, Gibs = GibSet.Human, Damage = 100, Range = 64,
	IdleSequence = "machete_idle1", Aliases = ["monster_t_jungle_kamakazi"] )]
public sealed class JungleBombCarrier : BombCarrier;

[Title( "Russian Bomb Carrier" )]
[NpcDefinition( "monster_terrorist_russian_kamakazi", "terror", MonsterClass.HumanMilitary, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Melee, Gibs = GibSet.Human, Damage = 100, Range = 64,
	IdleSequence = "machete_idle1", Aliases = ["monster_t_russian_kamakazi"] )]
public sealed class RussianBombCarrier : BombCarrier;

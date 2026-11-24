using GoldSrc.HalfLife;

namespace GoldSrc.ConditionZeroDeletedScenes;

[Title( "Urban Grenadier" )]
[NpcDefinition( "monster_terrorist_urban_grenader", "militia", MonsterClass.HumanMilitary, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Bullet, Damage = 5, Range = 1024,
	StrikeEvents = [4] )]
public sealed class UrbanGrenadier : Grenadier;

[Title( "Arctic Grenadier" )]
[NpcDefinition( "monster_terrorist_arctic_grenader", "arctic", MonsterClass.HumanMilitary, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Bullet, Damage = 5, Range = 1024,
	StrikeEvents = [4] )]
public sealed class ArcticGrenadier : Grenadier;

[Title( "Asian Grenadier" )]
[NpcDefinition( "monster_terrorist_asian_grenader", "asian", MonsterClass.HumanMilitary, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Bullet, Damage = 5, Range = 1024,
	StrikeEvents = [4] )]
public sealed class AsianGrenadier : Grenadier;

[Title( "Desert Grenadier" )]
[NpcDefinition( "monster_terrorist_desert_grenader", "leet", MonsterClass.HumanMilitary, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Bullet, Damage = 5, Range = 1024,
	StrikeEvents = [4] )]
public sealed class DesertGrenadier : Grenadier;

[Title( "Jungle Grenadier" )]
[NpcDefinition( "monster_terrorist_jungle_grenader", "guerilla", MonsterClass.HumanMilitary, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Bullet, Damage = 5, Range = 1024,
	StrikeEvents = [4] )]
public sealed class JungleGrenadier : Grenadier;

[Title( "Russian Grenadier" )]
[NpcDefinition( "monster_terrorist_russian_grenader", "terror", MonsterClass.HumanMilitary, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Bullet, Damage = 5, Range = 1024,
	StrikeEvents = [4] )]
public sealed class RussianGrenadier : Grenadier;

[Title( "GSG-9 Grenadier" )]
[NpcDefinition( "monster_ct_gsg9_grenader", "gsg9", MonsterClass.PlayerAlly, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Bullet, Damage = 5, Range = 1024,
	StrikeEvents = [4] )]
public sealed class Gsg9Grenadier : Grenadier;

[Title( "SAS Grenadier" )]
[NpcDefinition( "monster_ct_sas_grenader", "sas", MonsterClass.PlayerAlly, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Bullet, Damage = 5, Range = 1024,
	StrikeEvents = [4] )]
public sealed class SasGrenadier : Grenadier;

[Title( "GIGN Grenadier" )]
[NpcDefinition( "monster_ct_gign_grenader", "gign", MonsterClass.PlayerAlly, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Bullet, Damage = 5, Range = 1024,
	StrikeEvents = [4] )]
public sealed class GignGrenadier : Grenadier;

[Title( "SWAT Grenadier" )]
[NpcDefinition( "monster_ct_swat_grenader", "seal", MonsterClass.PlayerAlly, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Bullet, Damage = 5, Range = 1024,
	StrikeEvents = [4] )]
public sealed class SwatGrenadier : Grenadier;

[Title( "Spetsnaz Grenadier" )]
[NpcDefinition( "monster_ct_spetsnaz_grenader", "spetsnaz", MonsterClass.PlayerAlly, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Bullet, Damage = 5, Range = 1024,
	StrikeEvents = [4] )]
public sealed class SpetsnazGrenadier : Grenadier;

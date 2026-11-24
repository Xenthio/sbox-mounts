using GoldSrc.HalfLife;

namespace GoldSrc.ConditionZeroDeletedScenes;

[Title( "Urban Rifleman" )]
[NpcDefinition( "monster_terrorist_urban_assaultrifle", "militia", MonsterClass.HumanMilitary, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Bullet, StrikeEvents = [4, 5, 6], IdleSequence = "idle1",
	ClassNameVariants = ["monster_terrorist_urban_pistol", "monster_terrorist_urban_shotgun", "monster_terrorist_urban_mp5",
		"monster_terrorist_urban_smg", "monster_terrorist_urban_machinegun", "monster_terrorist_urban_sniperrifle"] )]
public sealed class UrbanRifleman : Rifleman;

[Title( "Arctic Rifleman" )]
[NpcDefinition( "monster_terrorist_arctic_assaultrifle", "arctic", MonsterClass.HumanMilitary, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Bullet, StrikeEvents = [4, 5, 6], IdleSequence = "idle1",
	ClassNameVariants = ["monster_terrorist_arctic_pistol", "monster_terrorist_arctic_shotgun", "monster_terrorist_arctic_mp5",
		"monster_terrorist_arctic_smg", "monster_terrorist_arctic_machinegun", "monster_terrorist_arctic_sniperrifle"] )]
public sealed class ArcticRifleman : Rifleman;

[Title( "Asian Rifleman" )]
[NpcDefinition( "monster_terrorist_asian_assaultrifle", "asian", MonsterClass.HumanMilitary, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Bullet, StrikeEvents = [4, 5, 6], IdleSequence = "idle1",
	ClassNameVariants = ["monster_terrorist_asian_pistol", "monster_terrorist_asian_shotgun", "monster_terrorist_asian_mp5",
		"monster_terrorist_asian_smg", "monster_terrorist_asian_machinegun", "monster_terrorist_asian_sniperrifle"] )]
public sealed class AsianRifleman : Rifleman;

[Title( "Desert Rifleman" )]
[NpcDefinition( "monster_terrorist_desert_assaultrifle", "leet", MonsterClass.HumanMilitary, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Bullet, StrikeEvents = [4, 5, 6], IdleSequence = "idle1",
	ClassNameVariants = ["monster_terrorist_desert_pistol", "monster_terrorist_desert_shotgun", "monster_terrorist_desert_mp5",
		"monster_terrorist_desert_smg", "monster_terrorist_desert_machinegun", "monster_terrorist_desert_sniperrifle"] )]
public sealed class DesertRifleman : Rifleman;

[Title( "Jungle Rifleman" )]
[NpcDefinition( "monster_terrorist_jungle_assaultrifle", "guerilla", MonsterClass.HumanMilitary, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Bullet, StrikeEvents = [4, 5, 6], IdleSequence = "idle1",
	ClassNameVariants = ["monster_terrorist_jungle_pistol", "monster_terrorist_jungle_shotgun", "monster_terrorist_jungle_mp5",
		"monster_terrorist_jungle_smg", "monster_terrorist_jungle_machinegun", "monster_terrorist_jungle_sniperrifle"] )]
public sealed class JungleRifleman : Rifleman;

[Title( "Russian Rifleman" )]
[NpcDefinition( "monster_terrorist_russian_assaultrifle", "terror", MonsterClass.HumanMilitary, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Bullet, StrikeEvents = [4, 5, 6], IdleSequence = "idle1",
	ClassNameVariants = ["monster_terrorist_russian_pistol", "monster_terrorist_russian_shotgun", "monster_terrorist_russian_mp5",
		"monster_terrorist_russian_smg", "monster_terrorist_russian_machinegun", "monster_terrorist_russian_sniperrifle"] )]
public sealed class RussianRifleman : Rifleman;

[Title( "GSG-9 Rifleman" )]
[NpcDefinition( "monster_ct_gsg9_assaultrifle", "gsg9", MonsterClass.PlayerAlly, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Bullet, StrikeEvents = [4, 5, 6], IdleSequence = "idle1",
	ClassNameVariants = ["monster_ct_gsg9_pistol", "monster_ct_gsg9_shotgun", "monster_ct_gsg9_mp5",
		"monster_ct_gsg9_smg", "monster_ct_gsg9_machinegun", "monster_ct_gsg9_sniperrifle"] )]
public sealed class Gsg9Rifleman : Rifleman;

[Title( "SAS Rifleman" )]
[NpcDefinition( "monster_ct_sas_assaultrifle", "sas", MonsterClass.PlayerAlly, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Bullet, StrikeEvents = [4, 5, 6], IdleSequence = "idle1",
	ClassNameVariants = ["monster_ct_sas_pistol", "monster_ct_sas_shotgun", "monster_ct_sas_mp5",
		"monster_ct_sas_smg", "monster_ct_sas_machinegun", "monster_ct_sas_sniperrifle"] )]
public sealed class SasRifleman : Rifleman;

[Title( "GIGN Rifleman" )]
[NpcDefinition( "monster_ct_gign_assaultrifle", "gign", MonsterClass.PlayerAlly, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Bullet, StrikeEvents = [4, 5, 6], IdleSequence = "idle1",
	ClassNameVariants = ["monster_ct_gign_pistol", "monster_ct_gign_shotgun", "monster_ct_gign_mp5",
		"monster_ct_gign_smg", "monster_ct_gign_machinegun", "monster_ct_gign_sniperrifle"] )]
public sealed class GignRifleman : Rifleman;

[Title( "SWAT Rifleman" )]
[NpcDefinition( "monster_ct_swat_assaultrifle", "seal", MonsterClass.PlayerAlly, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Bullet, StrikeEvents = [4, 5, 6], IdleSequence = "idle1",
	ClassNameVariants = ["monster_ct_swat_pistol", "monster_ct_swat_shotgun", "monster_ct_swat_mp5",
		"monster_ct_swat_smg", "monster_ct_swat_machinegun", "monster_ct_swat_sniperrifle"] )]
public sealed class SwatRifleman : Rifleman;

[Title( "Spetsnaz Rifleman" )]
[NpcDefinition( "monster_ct_spetsnaz_assaultrifle", "spetsnaz", MonsterClass.PlayerAlly, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Bullet, StrikeEvents = [4, 5, 6], IdleSequence = "idle1",
	ClassNameVariants = ["monster_ct_spetsnaz_pistol", "monster_ct_spetsnaz_shotgun", "monster_ct_spetsnaz_mp5",
		"monster_ct_spetsnaz_smg", "monster_ct_spetsnaz_machinegun", "monster_ct_spetsnaz_sniperrifle"] )]
public sealed class SpetsnazRifleman : Rifleman;

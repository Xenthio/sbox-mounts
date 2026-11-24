using GoldSrc.HalfLife;

namespace GoldSrc.ConditionZeroDeletedScenes;

[Title( "Urban LAW Soldier" )]
[NpcDefinition( "monster_terrorist_urban_law", "militia", MonsterClass.HumanMilitary, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Projectile, Damage = 125, Range = 2048,
	ProjectileModel = "lawrocket", StrikeEvents = [4], IdleSequence = "LAW_fire_idle" )]
public sealed class UrbanLawSoldier : LawSoldier;

[Title( "Arctic LAW Soldier" )]
[NpcDefinition( "monster_terrorist_arctic_law", "arctic", MonsterClass.HumanMilitary, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Projectile, Damage = 125, Range = 2048,
	ProjectileModel = "lawrocket", StrikeEvents = [4], IdleSequence = "LAW_fire_idle" )]
public sealed class ArcticLawSoldier : LawSoldier;

[Title( "Asian LAW Soldier" )]
[NpcDefinition( "monster_terrorist_asian_law", "asian", MonsterClass.HumanMilitary, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Projectile, Damage = 125, Range = 2048,
	ProjectileModel = "lawrocket", StrikeEvents = [4], IdleSequence = "LAW_fire_idle" )]
public sealed class AsianLawSoldier : LawSoldier;

[Title( "Desert LAW Soldier" )]
[NpcDefinition( "monster_terrorist_desert_law", "leet", MonsterClass.HumanMilitary, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Projectile, Damage = 125, Range = 2048,
	ProjectileModel = "lawrocket", StrikeEvents = [4], IdleSequence = "LAW_fire_idle" )]
public sealed class DesertLawSoldier : LawSoldier;

[Title( "Jungle LAW Soldier" )]
[NpcDefinition( "monster_terrorist_jungle_law", "guerilla", MonsterClass.HumanMilitary, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Projectile, Damage = 125, Range = 2048,
	ProjectileModel = "lawrocket", StrikeEvents = [4], IdleSequence = "LAW_fire_idle" )]
public sealed class JungleLawSoldier : LawSoldier;

[Title( "Russian LAW Soldier" )]
[NpcDefinition( "monster_terrorist_russian_law", "terror", MonsterClass.HumanMilitary, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Projectile, Damage = 125, Range = 2048,
	ProjectileModel = "lawrocket", StrikeEvents = [4], IdleSequence = "LAW_fire_idle" )]
public sealed class RussianLawSoldier : LawSoldier;

[Title( "GSG-9 LAW Soldier" )]
[NpcDefinition( "monster_ct_gsg9_law", "gsg9", MonsterClass.PlayerAlly, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Projectile, Damage = 125, Range = 2048,
	ProjectileModel = "lawrocket", StrikeEvents = [4], IdleSequence = "LAW_fire_idle" )]
public sealed class Gsg9LawSoldier : LawSoldier;

[Title( "SAS LAW Soldier" )]
[NpcDefinition( "monster_ct_sas_law", "sas", MonsterClass.PlayerAlly, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Projectile, Damage = 125, Range = 2048,
	ProjectileModel = "lawrocket", StrikeEvents = [4], IdleSequence = "LAW_fire_idle" )]
public sealed class SasLawSoldier : LawSoldier;

[Title( "GIGN LAW Soldier" )]
[NpcDefinition( "monster_ct_gign_law", "gign", MonsterClass.PlayerAlly, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Projectile, Damage = 125, Range = 2048,
	ProjectileModel = "lawrocket", StrikeEvents = [4], IdleSequence = "LAW_fire_idle" )]
public sealed class GignLawSoldier : LawSoldier;

[Title( "SWAT LAW Soldier" )]
[NpcDefinition( "monster_ct_swat_law", "seal", MonsterClass.PlayerAlly, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Projectile, Damage = 125, Range = 2048,
	ProjectileModel = "lawrocket", StrikeEvents = [4], IdleSequence = "LAW_fire_idle" )]
public sealed class SwatLawSoldier : LawSoldier;

[Title( "Spetsnaz LAW Soldier" )]
[NpcDefinition( "monster_ct_spetsnaz_law", "spetsnaz", MonsterClass.PlayerAlly, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Projectile, Damage = 125, Range = 2048,
	ProjectileModel = "lawrocket", StrikeEvents = [4], IdleSequence = "LAW_fire_idle" )]
public sealed class SpetsnazLawSoldier : LawSoldier;

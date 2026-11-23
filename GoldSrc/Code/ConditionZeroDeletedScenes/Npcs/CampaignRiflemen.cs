using GoldSrc.HalfLife;

namespace GoldSrc.ConditionZeroDeletedScenes;

[Title( "Urban Rifleman" )]
[NpcDefinition( "monster_terrorist_urban_assaultrifle", "terror", MonsterClass.HumanMilitary, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Bullet, Damage = 5, Range = 2048,
	AttackSequence = "standing_mp5", StrikeEvents = [4, 5, 6], IdleSequence = "idle1" )]
public sealed class UrbanRifleman : Rifleman
{
	internal override string RifleReport => "weapons/ak47-1.wav";
}

[Hide]
public abstract class CounterTerroristRifleman : Rifleman
{
	internal override string RifleReport => "weapons/m4a1-unsil-1.wav";
}

[Title( "GSG-9 Rifleman" )]
[NpcDefinition( "monster_ct_gsg9_assaultrifle", "gsg9", MonsterClass.PlayerAlly, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Bullet, Damage = 5, Range = 2048,
	AttackSequence = "standing_mp5", StrikeEvents = [4, 5, 6], IdleSequence = "idle1" )]
public sealed class Gsg9Rifleman : CounterTerroristRifleman;

[Title( "SAS Rifleman" )]
[NpcDefinition( "monster_ct_sas_assaultrifle", "sas", MonsterClass.PlayerAlly, 100, 16, 72, 180,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Attack = NpcAttack.Bullet, Damage = 5, Range = 2048,
	AttackSequence = "standing_mp5", StrikeEvents = [4, 5, 6], IdleSequence = "idle1" )]
public sealed class SasRifleman : CounterTerroristRifleman;

using GoldSrc.HalfLife;

namespace GoldSrc.CrackLife;

[Title( "Snarks" ), Category( "Crack-Life" )]
[WeaponPickup( "squeak", "snarks", 15, ClassName = "snark", PickupModel = "w_sqknest", Bucket = 4, Weight = 5, Mount = "cracklife" )]
public sealed class SnarkWeapon : CrackLifeSnarkWeapon;

[Title( "Snarks" ), Category( "Crack-Life: Campaign Mode" )]
[WeaponPickup( "squeak", "snarks", 15, ClassName = "snark", PickupModel = "w_sqknest", Bucket = 4, Weight = 5, Mount = "clcampaign" )]
public sealed class CampaignSnarkWeapon : CrackLifeSnarkWeapon;

[Hide]
public abstract class CrackLifeSnarkWeapon : ClassicSnarkWeapon
{
	internal override bool TryAimThrow( Ray ray, out Vector3 position, out Vector3 forward ) =>
		TryAimLegacyThrow( ray, out position, out forward );
}

[Title( "Snark" ), Category( "Crack-Life" )]
[NpcDefinition( "monster_snark", "w_squeak", MonsterClass.AlienWeapon, 2, 4, 8, 300, Mount = "cracklife",
	ShowInSpawnMenu = false, Movement = NpcMovement.Bouncing, Damage = 10, IdleSequence = "run", Blood = BloodColor.Yellow )]
public sealed class Snark : ClassicSnark;

[Title( "Snark" ), Category( "Crack-Life: Campaign Mode" )]
[NpcDefinition( "monster_snark", "w_squeak", MonsterClass.AlienWeapon, 2, 4, 8, 300, Mount = "clcampaign",
	ShowInSpawnMenu = false, Movement = NpcMovement.Bouncing, Damage = 10, IdleSequence = "run", Blood = BloodColor.Yellow )]
public sealed class CampaignSnark : ClassicSnark;

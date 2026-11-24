using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

[Title( "Snarks" ), Category( "Scientist Slaughterhouse" )]
[WeaponPickup( "squeak", "snarks", 100, ClassName = "snark", PickupModel = "w_sqknest", Bucket = 4, Weight = 5, Mount = "ssh" )]
public sealed class SnarkWeapon : ClassicSnarkWeapon
{
	internal override bool TryAimThrow( Ray ray, out Vector3 position, out Vector3 forward ) =>
		TryAimLegacyThrow( ray, out position, out forward );
}

[Title( "Snark" ), Category( "Scientist Slaughterhouse" )]
[NpcDefinition( "monster_snark", "w_squeak", MonsterClass.AlienWeapon, 2, 4, 8, 300, Mount = "ssh",
	ShowInSpawnMenu = false, Movement = NpcMovement.Bouncing, Damage = 10, IdleSequence = "run", Blood = BloodColor.Yellow )]
public sealed class Snark : ClassicSnark;

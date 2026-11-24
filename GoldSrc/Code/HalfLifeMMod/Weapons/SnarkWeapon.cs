using GoldSrc.HalfLife;

namespace GoldSrc.HalfLifeMMod;

[Title( "Snarks" ), Category( "Half-Life: MMod" )]
[WeaponPickup( "squeak", "snarks", 15, ClassName = "snark", PickupModel = "w_sqknest", Bucket = 4, Weight = 5, Mount = "hl1mmod" )]
public sealed class SnarkWeapon : ClassicSnarkWeapon
{
	bool _hasDeployed;

	internal override string DeploySequence => _hasDeployed ? "up" : "deploy_first";

	protected override void OnEquipped()
	{
		DeployTime = 0.5f;
		base.OnEquipped();
		_hasDeployed = true;
	}

	public override bool CanSecondaryAttack() => false;

	internal override bool TryAimThrow( Ray ray, out Vector3 position, out Vector3 forward ) =>
		TryAimLegacyThrow( ray, out position, out forward );

	internal override void PosePickup( SkinnedModelRenderer renderer )
	{
		renderer.UseAnimGraph = false;
		renderer.Sequence.Name = "idle";
		renderer.Sequence.Looping = true;
	}
}

[Title( "Snark" ), Category( "Half-Life: MMod" )]
[NpcDefinition( "monster_snark", "w_squeak", MonsterClass.AlienWeapon, 2, 4, 8, 300, Mount = "hl1mmod",
	ShowInSpawnMenu = false, Movement = NpcMovement.Bouncing, Damage = 10, IdleSequence = "run", Blood = BloodColor.Yellow )]
public sealed class Snark : ClassicSnark;

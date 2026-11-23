using GoldSrc.HalfLife;

namespace GoldSrc.CrackLife;

[Title( "Scientist Head" ), Category( "Crack-Life" )]
[WeaponPickup( "scihead", null, 0, PickupModel = "w_crowbar", Bucket = 0, Weight = 0, Mount = "cracklife" )]
public sealed class ScientistHead : ClassicCrowbar
{
	internal override string WorldModelPath => "models/p_crowbar.mdl";
	internal override float HitDelay => 1;

	internal override void Configure()
	{
		base.Configure();
		PrimaryDelay = 1;
		Ballistics = Ballistics with { Damage = 300 };
	}

	internal override float SwingDamage( bool fresh ) =>
		fresh || Networking.MaxPlayers > 1 ? Ballistics.Damage : Ballistics.Damage * 0.5f;

	public override bool CanSecondaryAttack() => false;
}

using GoldSrc.HalfLife;

namespace GoldSrc.HalfLifeMMod;

[Title( "Laser Tripmine" ), Category( "Half-Life: MMod" )]
[WeaponPickup( "tripmine", "tripmines", 5, PickupModel = "v_tripmine", PreviewModel = "p_tripmine", Bucket = 4, Weight = -10, Mount = "hl1mmod" )]
public sealed class Tripmine : ClassicTripmine
{
	bool _hasDeployed;
	TimeUntil _placementEnds;

	internal override string DeploySequence => _hasDeployed ? "draw" : "draw_first";
	internal override string ShotSequence => "place";
	protected override bool CanRetire => _placementEnds <= 0;

	protected override void OnEquipped()
	{
		DeployTime = 0.5f;
		base.OnEquipped();
		_hasDeployed = true;
	}

	internal override void Configure()
	{
		base.Configure();
		PrimaryDelay = 0.5f;
	}

	public override bool CanSecondaryAttack() => false;

	internal override bool Release( Ray ray )
	{
		if ( !base.Release( ray ) ) return false;
		_placementEnds = PrimaryDelay;
		return true;
	}
}

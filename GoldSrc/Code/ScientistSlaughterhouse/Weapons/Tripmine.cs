using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

[Title( "Laser Tripmine" ), Category( "Scientist Slaughterhouse" )]
[WeaponPickup( "tripmine", "tripmines", 50, PickupModel = "v_tripmine", PreviewModel = "p_tripmine", Bucket = 4, Weight = -10, Mount = "ssh" )]
public sealed class Tripmine : ClassicTripmine
{
	internal override void Configure()
	{
		base.Configure();
		StartingAmmo = 5;
		SecondaryDelay = PrimaryDelay;
	}

	public override bool HasSecondaryAmmo() => HasPrimaryAmmo();
	public override void SecondaryAttack() => PlaceCannon( AimRay );

	[Rpc.Host( NetFlags.OwnerOnly )]
	void PlaceCannon( Ray ray )
	{
		if ( !IsPrimaryLaunchReady || !TryLaunch( ray, ReleaseCannon, secondary: true ) ) return;
		SetNextFire( PrimaryDelay );
	}

	internal override bool Release( Ray ray )
	{
		if ( !base.Release( ray ) ) return false;
		SetNextFire( PrimaryDelay );
		return true;
	}

	bool ReleaseCannon( Ray ray )
	{
		var hit = Scene.Trace.Ray( ray, 128 ).IgnoreGameObjectHierarchy( Owner.GameObject ).Run();
		if ( !CanPlace( hit ) ) return false;
		return PlacedCharge.Attach<TripmineTurret>( Owner, hit, ContentMount );
	}
}

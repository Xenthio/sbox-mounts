using GoldSrc.HalfLife;

namespace GoldSrc.CrackLife;

[Title( "Crossbow" ), Category( "Crack-Life" )]
[WeaponPickup( "crossbow", "bolts", 50, Bucket = 2, Weight = 10, Mount = "cracklife" )]
public sealed class Crossbow : CrackLifeCrossbow;

[Title( "Crossbow" ), Category( "Crack-Life: Campaign Mode" )]
[WeaponPickup( "crossbow", "bolts", 500, Bucket = 2, Weight = 10, Mount = "clcampaign" )]
public sealed class CampaignCrossbow : CrackLifeCrossbow
{
	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 50;
		PrimaryDelay = 0.075f;
	}

	// The original still passes the stock five-round size to its reload check.
	public override bool CanReload() => Clip1 != 5 && base.CanReload();
}

[Hide]
public abstract class CrackLifeCrossbow : ClassicCrossbow
{
	public override Ray AimRay => Owner.IsValid() ? OffsetLaunchRay( new Vector3( 0, 0, -2 ) ) : base.AimRay;

	internal override CrossbowBolt SpawnBolt( Ray ray, float speed )
	{
		if ( ContentMount is not { } mount || mount.FindMountPath( "models/crossbow_bolt.mdl" ) is not { } path ) return null;
		return NpcProjectile.Fire<CrossbowBolt>( Owner.GameObject, $"mount://{mount.Ident}/{path}.vmdl",
			50, null, ray.Position, ray.Forward, speed );
	}
}

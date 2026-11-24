using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

[Title( ".357 Magnum" ), Category( "Scientist Slaughterhouse" )]
[WeaponPickup( "357", "357", 72, Bucket = 1, Weight = 15, Mount = "ssh" )]
public sealed class Revolver : ClassicRevolver
{
	internal override float ShotDamage => 40;
	internal override int FreshPickupRounds => 12;
	internal override bool HasScope => true;

	public override Ray AimRay => Owner.IsValid() ? Owner.EyeTransform.ForwardRay : base.AimRay;
}

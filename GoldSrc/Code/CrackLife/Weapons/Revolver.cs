using GoldSrc.HalfLife;

namespace GoldSrc.CrackLife;

[Title( ".357 Magnum" ), Category( "Crack-Life" )]
[WeaponPickup( "357", "357", 36, Bucket = 1, Weight = 15, Mount = "cracklife" )]
public sealed class Revolver : CrackLifeRevolver
{
	internal override float ShotDamage => 30000;
}

[Title( ".357 Magnum" ), Category( "Crack-Life: Campaign Mode" )]
[WeaponPickup( "357", "357", 36, Bucket = 1, Weight = 15, Mount = "clcampaign" )]
public sealed class CampaignRevolver : CrackLifeRevolver
{
	internal override float ShotDamage => 350;
}

[Hide]
public abstract class CrackLifeRevolver : ClassicRevolver
{
	public override Ray AimRay => Owner.IsValid() ? Owner.EyeTransform.ForwardRay : base.AimRay;
	internal override (string Name, float? Delay) IdleAnimation => ("idle1", null);
}

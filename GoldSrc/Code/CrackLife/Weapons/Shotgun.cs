using GoldSrc.HalfLife;

namespace GoldSrc.CrackLife;

[Title( "Shotgun" ), Category( "Crack-Life" )]
[WeaponPickup( "shotgun", "buckshot", 125, Bucket = 2, Weight = 15, Mount = "cracklife" )]
public sealed class Shotgun : CrackLifeShotgun
{
	internal override float PelletDamage => 5;
}

[Title( "Shotgun" ), Category( "Crack-Life: Campaign Mode" )]
[WeaponPickup( "shotgun", "buckshot", 200, Bucket = 2, Weight = 15, Mount = "clcampaign" )]
public sealed class CampaignShotgun : CrackLifeShotgun
{
	internal override float PelletDamage => CampaignBuckshotDamage;
}

[Hide]
public abstract class CrackLifeShotgun : PumpShotgun
{
	internal const float CampaignBuckshotDamage = 65;

	public override Ray AimRay => Owner.IsValid() ? Owner.EyeTransform.ForwardRay : base.AimRay;
	internal abstract float PelletDamage { get; }
	internal override (float Damage, int Pellets) ShellDamage => (PelletDamage, Deathmatch ? 4 : 6);
	internal override Vector2 PelletSpread => Deathmatch
		? new Vector2( DoubleShot ? 0.17365f : 0.08716f, 0.04362f )
		: base.PelletSpread;
}

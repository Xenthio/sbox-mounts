using GoldSrc.HalfLife;

namespace GoldSrc.CrackLife;

[Title( "9mm Handgun" ), Category( "Crack-Life" )]
[WeaponPickup( "9mmhandgun", "9mm", 250, Bucket = 1, Weight = 10, Mount = "cracklife" )]
public sealed class Pistol : CrackLifePistol;

[Title( "9mm Handgun" ), Category( "Crack-Life: Campaign Mode" )]
[WeaponPickup( "9mmhandgun", "9mm", 250, Bucket = 1, Weight = 10, Mount = "clcampaign" )]
public sealed class CampaignPistol : CrackLifePistol;

[Hide]
public abstract class CrackLifePistol : ClassicPistol
{
	internal override bool CanIdle => true;
	internal override string WorldModelPath => "models/p__9mmhandgun.mdl";
	internal override (string Name, float? Delay) IdleAnimation => ("idle", null);
	internal override string ShotSequence => "shoot";
	internal override string ReloadSequence => "reload";
}

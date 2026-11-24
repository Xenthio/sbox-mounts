using GoldSrc.HalfLife;

namespace GoldSrc.CrackLife;

[Title( "Satchel Charge" ), Category( "Crack-Life" )]
[WeaponPickup( "satchel", "satchels", 5, AlternateModel = "satchel_radio", Bucket = 4, Weight = -10, Mount = "cracklife" )]
public sealed class Satchel : CrackLifeSatchel;

[Title( "Satchel Charge" ), Category( "Crack-Life: Campaign Mode" )]
[WeaponPickup( "satchel", "satchels", 5, AlternateModel = "satchel_radio", Bucket = 4, Weight = -10, Mount = "clcampaign" )]
public sealed class CampaignSatchel : CrackLifeSatchel;

[Hide]
public abstract class CrackLifeSatchel : ClassicSatchel
{
	internal override bool DetonateWithPrimary => true;
	internal override GameMount ChargeMount => ContentMount;

	public override Ray AimRay => Owner.IsValid()
		? new Ray( Owner.WorldPosition + Vector3.Up * (Owner.CurrentHeight * 0.5f), Owner.EyeTransform.Forward )
		: base.AimRay;
}

using GoldSrc.HalfLife;

namespace GoldSrc.CrackLife;

[Title( "Crowbar" ), Category( "Crack-Life" )]
[WeaponPickup( "crowbar", null, 0, Mount = "cracklife" )]
public sealed class Crowbar : CrackLifeCrowbar;

[Title( "Crowbar" ), Category( "Crack-Life: Campaign Mode" )]
[WeaponPickup( "crowbar", null, 0, Mount = "clcampaign" )]
public sealed class CampaignCrowbar : CrackLifeCrowbar;

[Hide]
public abstract class CrackLifeCrowbar : ClassicCrowbar
{
	internal override void Configure()
	{
		base.Configure();
		Ballistics = Ballistics with { Damage = 60 };
		SecondaryDelay = 2;
		SecondaryAutomatic = false;
	}

	internal override float SwingDamage( bool fresh ) =>
		fresh || Networking.MaxPlayers > 1 ? Ballistics.Damage : Ballistics.Damage * 0.5f;

	public override void SecondaryAttack()
	{
		PlaySample( "taunts/taunt" );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( "taunt" );
	}
}

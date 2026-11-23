using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Title( "MG34" ), Category( "Day of Defeat" )]
[WeaponPickup( "mg34", "ammo_66mm", 450, Bucket = 2, Weight = 40, Mount = "dod" )]
public sealed class Mg34 : MachineGun
{
	internal override int FreshPickupRounds => 450;
	internal override float CarriedReloadTime => 5.7f;
	internal override string DeploySequence => BipodSequence( false );
	internal override (string Name, float? Delay) IdleAnimation => (WithAmmo( BipodDeployed ? "downidle" : "upidle" ), 2.5f);
	internal override string ShotSequence => WithAmmo( BipodDeployed ? "downshoot" : "upshoot" );
	internal override string BipodSequence( bool deployed ) => WithAmmo( deployed ? "uptodown" : "downtoup" );

	string WithAmmo( string sequence ) => Clip1 == 0 ? $"{sequence}_empty" : sequence;

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 75;
	}
}

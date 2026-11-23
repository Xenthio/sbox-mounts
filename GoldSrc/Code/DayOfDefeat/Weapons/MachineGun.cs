namespace GoldSrc.DayOfDefeat;

[Hide]
public abstract class MachineGun : BipodRifle
{
	internal override string CarriedModel => $"p_{Definition.Model}bu";
	internal override string DeployedModel => $"p_{Definition.Model}bd";
	internal override string ReloadSequence => "reload";
	internal override float BipodTime => 0.6f;
	internal override float StandingSpread => 0.2f;
	internal override bool EjectsBrass => false;
	internal override int PlayerMuzzleFlash => 4;

	internal override void Configure()
	{
		base.Configure();
		DeployTime = 0.5f;
	}

	public override void PrimaryAttack()
	{
		SetNextSecondaryFire( PrimaryDelay );
		base.PrimaryAttack();
	}
}

[Hide]
public abstract class BeltFedMachineGun : MachineGun
{
	internal override string DeploySequence => BipodSequence( false );
	internal override (string Name, float? Delay) IdleAnimation => ($"{Stance}idle{BeltSuffix}", null);
	internal override string ShotSequence => $"{Stance}shoot{ShotBeltSuffix}";
	internal override string BipodSequence( bool deployed ) => (deployed ? "uptodown" : "downtoup") + BeltSuffix;

	string Stance => BipodDeployed ? "down" : "up";
	string BeltSuffix => Clip1 == 0 ? "empty" : Clip1 is >= 1 and <= 8 ? Clip1.ToString() : "";
	// These shots animate the last visible cartridge even when the clip is already empty.
	string ShotBeltSuffix => Clip1 == 0 ? "1" : BeltSuffix;
}

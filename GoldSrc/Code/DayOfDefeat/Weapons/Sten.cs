using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Title( "Sten" ), Category( "Day of Defeat" )]
[WeaponPickup( "sten", "ammo_44mm", 210, Bucket = 2, Weight = 20, Mount = "dod" )]
public sealed class Sten : DayOfDefeatAutomatic
{
	internal override string ShotSequence => "shoot";
	internal override (string Name, float? Delay) IdleAnimation => ("idle", 2.5f);

	internal override void Configure()
	{
		base.Configure();
		DeployTime = 1;
		PrimaryDelay = 0.085f;
		ReloadTime = 2.9f;
	}
}

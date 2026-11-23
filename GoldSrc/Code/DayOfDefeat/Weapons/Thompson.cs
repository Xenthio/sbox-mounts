using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Title( "M1 Thompson" ), Category( "Day of Defeat" )]
[WeaponPickup( "tommy", "ammo_44mm", 210, ClassName = "thompson", Bucket = 2, Weight = 20, Mount = "dod" )]
public sealed class Thompson : DayOfDefeatAutomatic
{
	internal override string ShotSound => "weapons/thompson_shoot.wav";

	internal override void Configure()
	{
		base.Configure();
		DeployTime = 1.1f;
		PrimaryDelay = 0.085f;
		ReloadTime = 2.8f;
	}
}

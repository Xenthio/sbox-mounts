using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Title( "MP40" ), Category( "Day of Defeat" )]
[WeaponPickup( "mp40", "ammo_44mm", 210, Bucket = 2, Weight = 20, Mount = "dod" )]
public sealed class Mp40 : DayOfDefeatAutomatic
{
	internal override void Configure()
	{
		base.Configure();
		DeployTime = 1;
		PrimaryDelay = 0.09f;
		ReloadTime = 2.85f;
	}
}

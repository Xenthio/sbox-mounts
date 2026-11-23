using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Title( "StG 44" ), Category( "Day of Defeat" )]
[WeaponPickup( "mp44", "ammo_55mm", 210, Bucket = 2, Weight = 20, Mount = "dod" )]
public sealed class Mp44 : DayOfDefeatAutomatic
{
	internal override int PlayerMuzzleFlash => 41;
	internal override int BrassBodyGroup => 1;
	internal override string WorldModelPath => "models/p_stg44.mdl";

	internal override void Configure()
	{
		base.Configure();
		HoldType = WeaponHolds.Rifle;
		DeployTime = 1;
		PrimaryDelay = 0.09f;
		ReloadTime = 3.13f;
		Ballistics = Ballistics with { Damage = 50 };
	}
}

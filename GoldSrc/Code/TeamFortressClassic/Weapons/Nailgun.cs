using GoldSrc.HalfLife;

namespace GoldSrc.TeamFortressClassic;

[Title( "Nailgun" ), Category( "Team Fortress Classic" )]
[WeaponPickup( "tfc_nailgun", "9mm", 200, EntityName = "tf_weapon_ng", PickupModel = "p_nailgun", Bucket = 3, Mount = "tfc" )]
public sealed class Nailgun : NailWeapon
{
	internal override string WorldModelPath => "models/p_nailgun.mdl";

	internal override void PlayLaunchSound()
	{
		base.PlayLaunchSound();
		PlaySample( "weapons/airgun_1.wav", Game.Random.Float( 0.95f, 1 ), Game.Random.Int( 93, 124 ) / 100f );
	}
}

[Title( "Super Nailgun" ), Category( "Team Fortress Classic" )]
[WeaponPickup( "tfc_supernailgun", "9mm", 200, EntityName = "tf_weapon_superng", PickupModel = "p_snailgun", Bucket = 3, Mount = "tfc" )]
public sealed class SuperNailgun : NailWeapon
{
	internal override string WorldModelPath => "models/p_snailgun.mdl";

	internal override bool Release( Ray ray )
	{
		if ( !ReleaseNail( ray, Ammo1 <= 3 ? 9 : 13 ) ) return false;
		if ( Ammo1 >= 2 ) TakePrimaryAmmo();
		return true;
	}

	internal override void PlayLaunchSound()
	{
		base.PlayLaunchSound();
		PlaySample( "weapons/spike2.wav", 1, Game.Random.Int( 94, 109 ) / 100f );
	}
}

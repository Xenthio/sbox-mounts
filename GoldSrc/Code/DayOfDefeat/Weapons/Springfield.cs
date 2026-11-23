using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Title( "Springfield" ), Category( "Day of Defeat" )]
[WeaponPickup( "spring", "ammo_22mm", 55, Bucket = 2, Weight = 20, Mount = "dod" )]
public sealed class Springfield : ScopedRifle
{
	internal override string ScopeSprite => "scope_spring";
	internal override float ScopeReturnDelay => 1.4f;
	internal override int FreshPickupRounds => 55;
	internal override float BrassDelay => 34f / 35f;
	internal override string ShotSequence => Game.Random.Int( 0, 1 ) == 0 ? "shoot1" : "shoot2";

	internal override void Configure()
	{
		base.Configure();
		PrimaryDelay = 1.85f;
		DeployTime = 1.33f;
		ReloadTime = 2.7f;
	}
}

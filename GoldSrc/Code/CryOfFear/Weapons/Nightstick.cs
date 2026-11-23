using GoldSrc.HalfLife;

namespace GoldSrc.CryOfFear;

[Title( "Nightstick" ), Category( "Cry of Fear" )]
[WeaponPickup( "nightstick", null, 0, Bucket = 2, Weight = 1, ModelDirectory = "models/weapons/nightstick", Mount = "cof" )]
public sealed class Nightstick : MeleeWeapon
{
	internal override float StrikeDelay => 0.31f;
	internal override Angles StrikePunch => new( -3, -8, 0 );
	internal override string WorldModelPath => "police/models/weapons/nightstick/p_nightstick.mdl";

	internal override void Configure()
	{
		base.Configure();
		HoldType = WeaponHolds.Melee;
		PrimaryDelay = 1;
		Ballistics = new() { Damage = 11, Range = 32, Radius = 0 };
	}

	public override void PrimaryAttack() => BeginSwing( $"attack{Game.Random.Int( 1, 3 )}", Ballistics.Damage, PrimaryDelay );
	public override bool CanSecondaryAttack() => false;
}

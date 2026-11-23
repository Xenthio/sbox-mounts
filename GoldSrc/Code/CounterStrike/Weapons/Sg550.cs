using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Title( "SG 550" ), Category( "Counter-Strike" )]
[WeaponPickup( "sg550", "556nato", 90, Bucket = 0, Weight = 30, Mount = "cstrike" )]
public sealed class Sg550 : Sg550Weapon;

[Title( "SG 550" ), Category( "Counter-Strike: Condition Zero" )]
[WeaponPickup( "sg550", "556nato", 90, Bucket = 0, Weight = 30, Mount = "czero" )]
public sealed class ConditionZeroSg550 : Sg550Weapon;

[Hide]
public abstract class Sg550Weapon : SemiAutomaticSniper
{
	internal override float RecoveryRate => 0.35f;
	internal override float RapidFireAccuracy => 0.65f;
	internal override float StartingAccuracy => 0.9f;
	internal override float RangeModifier => 0.98f;
	internal override float IdleAfterReloadStart => Definition.Mount == "czero" ? 3.55f : 3.8f;
	internal override Angles ShotRecoil => new( -Game.Random.Float( 0.75f, 1.25f ), Game.Random.Float( -0.75f, 0.75f ), 0 );

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 30;
		ReloadTime = 3.35f;
		Ballistics = new() { Damage = 70, Range = 8192, Radius = 0 };
	}

	internal override float CalculateSpread( bool grounded, bool crouched, float speed, bool zoomed )
	{
		var inaccuracy = 1 - Accuracy;
		var stance = !grounded ? 0.45f * inaccuracy : speed switch
		{
			> 0 => 0.15f,
			_ => (crouched ? 0.04f : 0.05f) * inaccuracy
		};
		RecoverAccuracy( Accuracy );
		return stance + (zoomed ? 0 : HipFireSpread);
	}
}

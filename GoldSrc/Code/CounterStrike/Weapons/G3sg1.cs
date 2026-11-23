using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Title( "G3SG1" ), Category( "Counter-Strike" )]
[WeaponPickup( "g3sg1", "762nato", 90, Bucket = 0, Weight = 30, Mount = "cstrike" )]
public sealed class G3sg1 : G3sg1Weapon;

[Title( "G3SG1" ), Category( "Counter-Strike: Condition Zero" )]
[WeaponPickup( "g3sg1", "762nato", 90, Bucket = 0, Weight = 30, Mount = "czero" )]
public sealed class ConditionZeroG3sg1 : G3sg1Weapon;

[Hide]
public abstract class G3sg1Weapon : SemiAutomaticSniper
{
	internal override float RecoveryRate => 0.3f;
	internal override float RapidFireAccuracy => 0.55f;
	internal override float StartingAccuracy => 0.2f;
	internal override float RangeModifier => 0.98f;
	internal override float IdleAfterReloadStart => Definition.Mount == "czero" ? 3.6f : 4.7f;
	internal override Angles ShotRecoil => new( -Game.Random.Float( 0.75f, 1.75f ), Game.Random.Float( -0.75f, 0.75f ), 0 );

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 20;
		ReloadTime = 3.5f;
		Ballistics = new() { Damage = 80, Range = 8192, Radius = 0 };
	}

	internal override float CalculateSpread( bool grounded, bool crouched, float speed, bool zoomed )
	{
		var stance = !grounded ? 0.45f : speed > 0 ? 0.15f : crouched ? 0.035f : 0.055f;
		RecoverAccuracy( 0.98f );
		return (stance + (zoomed ? 0 : HipFireSpread)) * (1 - Accuracy);
	}

	protected override void OnReloadStarted()
	{
		Accuracy = StartingAccuracy;
		base.OnReloadStarted();
	}
}

using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Title( "Bazooka" ), Category( "Day of Defeat" )]
[WeaponPickup( "bazooka", "rockets", 5, Mount = "dod", Bucket = 2, Weight = 20 )]
public sealed class Bazooka : DayOfDefeatLauncher
{
	internal override string CarriedModel => "p_bazooka";
	internal override string ShellModel => "w_bazooka_rocket";
	internal override float CarriedReloadTime => 3;
	internal override float FireDelay => 3;
	internal override float? IdleAfterShot => 0.5f;
}

[Title( "Panzerschreck" ), Category( "Day of Defeat" )]
[WeaponPickup( "panzerschreck", "rockets", 5, Mount = "dod", ClassName = "pschreck", PickupModel = "w_pschreck", Bucket = 2, Weight = 20 )]
public sealed class Panzerschreck : DayOfDefeatLauncher
{
	internal override string CarriedModel => "p_pschreck";
	internal override string ShellModel => "w_pschreck_rocket";
	internal override float CarriedReloadTime => 3.5f;
	internal override float ShoulderedReloadTime => 3;
	internal override float FireDelay => 1.5f;
	internal override float? IdleAfterShot => 1.17f;
}

[Title( "PIAT" ), Category( "Day of Defeat" )]
[WeaponPickup( "piat", "rockets", 5, Mount = "dod", Bucket = 2, Weight = 20 )]
public sealed class Piat : DayOfDefeatLauncher
{
	internal override string CarriedModel => "p_piat";
	internal override string ShellModel => "w_piat_rocket";
	internal override float CarriedReloadTime => 3.1f;
	internal override float FireDelay => 1;
	internal override bool HasEmptySequences => true;
}

using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Title( "Allied Combat Knife" ), Category( "Day of Defeat" )]
[WeaponPickup( "amerk", null, 0, Weight = 0, Mount = "dod", ClassName = "amerknife" )]
public sealed class AlliedKnife : DayOfDefeatMelee;

[Title( "Axis Paratrooper Knife" ), Category( "Day of Defeat" )]
[WeaponPickup( "paraknife", null, 0, Weight = 0, Mount = "dod", ClassName = "gerknife" )]
public sealed class ParatrooperKnife : DayOfDefeatMelee;

[Title( "Fairbairn-Sykes Knife" ), Category( "Day of Defeat" )]
[WeaponPickup( "fairbairn", null, 0, Weight = 0, Mount = "dod", PickupModel = "p_fairbairn" )]
public sealed class FairbairnKnife : DayOfDefeatMelee;

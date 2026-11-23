using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Title( "Mk II Hand Grenade" ), Category( "Day of Defeat" )]
[WeaponPickup( "grenade", "ammo_agrens", 3, ClassName = "handgrenade", Bucket = 4, Weight = 1, Mount = "dod" )]
public sealed class HandGrenade : DayOfDefeatGrenadeWeapon;

[Title( "Mills Bomb" ), Category( "Day of Defeat" )]
[WeaponPickup( "mills", "ammo_agrens", 3, Bucket = 4, Weight = 1, Mount = "dod" )]
public sealed class MillsBomb : DayOfDefeatGrenadeWeapon;

[Title( "Stick Grenade" ), Category( "Day of Defeat" )]
[WeaponPickup( "stick", "ammo_ggrens", 3, ClassName = "stickgrenade", Bucket = 4, Weight = 1, Mount = "dod" )]
public sealed class StickGrenade : DayOfDefeatGrenadeWeapon;

[Title( "Live Mk II Hand Grenade" ), Category( "Day of Defeat" )]
[WeaponPickup( "grenade", null, 0, ClassName = "handgrenade_ex", Bucket = 4, Weight = 1, Mount = "dod" )]
public sealed class LiveHandGrenade : LiveGrenadeWeapon;

[Title( "Live Mills Bomb" ), Category( "Day of Defeat" )]
[WeaponPickup( "mills", null, 0, ClassName = "mills_ex", Bucket = 4, Weight = 1, Mount = "dod" )]
public sealed class LiveMillsBomb : LiveGrenadeWeapon;

[Title( "Live Stick Grenade" ), Category( "Day of Defeat" )]
[WeaponPickup( "stick", null, 0, ClassName = "stickgrenade_ex", Bucket = 4, Weight = 1, Mount = "dod" )]
public sealed class LiveStickGrenade : LiveGrenadeWeapon;

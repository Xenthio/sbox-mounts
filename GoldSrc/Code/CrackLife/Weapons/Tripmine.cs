using GoldSrc.HalfLife;

namespace GoldSrc.CrackLife;

[Title( "Laser Tripmine" ), Category( "Crack-Life" )]
[WeaponPickup( "tripmine", "tripmines", 5, PickupModel = "v_tripmine", PreviewModel = "p_tripmine", Bucket = 4, Weight = -10, Mount = "cracklife" )]
public sealed class Tripmine : ClassicTripmine;

[Title( "Laser Tripmine" ), Category( "Crack-Life: Campaign Mode" )]
[WeaponPickup( "tripmine", "tripmines", 5, PickupModel = "v_tripmine", PreviewModel = "p_tripmine", Bucket = 4, Weight = -10, Mount = "clcampaign" )]
public sealed class CampaignTripmine : ClassicTripmine;

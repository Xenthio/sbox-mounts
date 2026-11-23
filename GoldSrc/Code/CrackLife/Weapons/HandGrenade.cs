using GoldSrc.HalfLife;

namespace GoldSrc.CrackLife;

[Title( "Hand Grenade" ), Category( "Crack-Life" )]
[WeaponPickup( "grenade", "grenades", 10, ClassName = "handgrenade", Bucket = 4, Weight = 5, Mount = "cracklife" )]
public sealed class HandGrenade : CrackLifeHandGrenade;

[Title( "Hand Grenade" ), Category( "Crack-Life: Campaign Mode" )]
[WeaponPickup( "grenade", "grenades", 60, ClassName = "handgrenade", Bucket = 4, Weight = 5, Mount = "clcampaign" )]
public sealed class CampaignHandGrenade : CrackLifeHandGrenade;

[Hide]
public abstract class CrackLifeHandGrenade : ClassicHandGrenade
{
	internal override void SpawnGrenade( Vector3 origin, Vector3 velocity, float fuse )
	{
		if ( ContentMount is not { } mount || mount.FindMountPath( "models/w_grenade.mdl" ) is not { } path ) return;
		TimedGrenade.Spawn<TimedGrenade>( Owner.GameObject, origin, velocity, fuse, "Hand grenade",
			$"mount://{mount.Ident}/{path}.vmdl", effectsMount: mount );
	}
}

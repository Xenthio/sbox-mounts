using GoldSrc.HalfLife;

namespace GoldSrc.TeamFortressClassic;

[Title( "Fragmentation Grenade" ), Category( "Team Fortress Classic" )]
[WeaponPickup( "grenade", "tfc_frag_grenades", 4, EntityName = "tf_weapon_normalgrenade", Bucket = 4, Mount = "tfc" )]
public sealed class FragGrenade : FortressHandGrenadeWeapon
{
	internal override void Configure()
	{
		base.Configure();
		StartingAmmo = 4;
	}

	internal override string LaunchGrenade( Vector3 origin, Vector3 direction, float pitch, float pinAge ) =>
		Launch<FortressFragGrenade>( origin, pinAge, "TFC fragmentation grenade", "models/w_grenade.mdl" );
}

[Hide]
public sealed class FortressFragGrenade : FortressExplosiveGrenade;

[Hide]
public abstract class FortressExplosiveGrenade : FortressHandGrenade
{
	const float Damage = 180;

	internal override void Detonate()
	{
		var position = WorldPosition;
		var floor = TraceFloor();
		var damageOrigin = !InHand && floor.Hit ? floor.EndPosition + floor.Normal * 32 : position;
		var effectOrigin = floor.Hit ? floor.EndPosition + floor.Normal * ((Damage - 24) * 0.6f) : position;
		FortressBlast.Apply( GameObject, Thrower, damageOrigin, Damage, falloff: 0.5f, inHand: InHand );
		Mount?.NpcAudio.Emit( $"weapons/explode{Game.Random.Int( 3, 5 )}.wav", effectOrigin );
		Mount?.NpcAudio.Emit( "weapons/debris", effectOrigin, volume: 0.55f, attenuation: 0.8f );
		ShowExplosion( effectOrigin, floor.Normal );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void ShowExplosion( Vector3 position, Vector3 normal )
	{
		if ( Mount is not { } mount ) return;
		Blast.SpriteEffect( "TFC grenade explosion", "explode01", position, (Damage - 50) * 0.06f, 15,
			additive: false, mount: mount );
		if ( WaterBody.Find( Game.ActiveScene, position ).IsValid() ) return;
		var count = Game.Random.Int( 0, 3 );
		for ( var i = 0; i < count; i++ )
			SparkShower.Spawn( position, normal, mount );
	}
}

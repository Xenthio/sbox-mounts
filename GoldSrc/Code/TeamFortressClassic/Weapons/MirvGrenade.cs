using GoldSrc.HalfLife;

namespace GoldSrc.TeamFortressClassic;

[Title( "MIRV Grenade" ), Category( "Team Fortress Classic" )]
[WeaponPickup( "grenade", "tfc_mirv_grenades", 2, EntityName = "tf_weapon_mirvgrenade", PickupModel = "mirv_grenade", Bucket = 4, Mount = "tfc" )]
public sealed class MirvGrenade : FortressHandGrenadeWeapon
{
	internal override void Configure()
	{
		base.Configure();
		StartingAmmo = 2;
	}

	internal override string LaunchGrenade( Vector3 origin, Vector3 direction, float pitch, float pinAge ) =>
		Launch<FortressMirvGrenade>( origin, pinAge, "TFC MIRV grenade", "models/mirv_grenade.mdl" );
}

[Hide]
public sealed class FortressMirvGrenade : FortressExplosiveGrenade
{
	const int BombletCount = 4;

	internal override void Detonate()
	{
		if ( Mount?.FindFile( "models/bomblet.mdl" ) is { } model )
		{
			var bomblets = new List<FortressBomblet>( BombletCount );
			var origin = WorldPosition + Vector3.Up * 32;
			for ( var i = 0; i < BombletCount; i++ )
			{
				var velocity = new Vector3( Game.Random.Float( -150, 150 ), Game.Random.Float( -150, 150 ), Game.Random.Float( 0, 200 ) );
				var bomblet = Spawn<FortressBomblet>( Thrower, origin, velocity, Game.Random.Float( 2, 3 ),
					"TFC MIRV bomblet", $"mount://tfc/{model}.vmdl", item =>
					{
						foreach ( var sibling in bomblets )
							item.AddComponent<PhysicsFilter>().Body = sibling.GameObject;
					} );
				bomblets.Add( bomblet );
			}
		}
		base.Detonate();
	}
}

[Hide]
public sealed class FortressBomblet : FortressExplosiveGrenade
{
	internal override float GravityScale => 0.5f;
	internal override float Friction => 0.8f;
}

using GoldSrc.HalfLife;

namespace GoldSrc.CrackLife;

[Title( "Cluster Grenade" ), Category( "Crack-Life" )]
[WeaponPickup( "grenade", "cluster_grenades", 50, ClassName = "clustergrenade", Bucket = 4, Weight = 8, Mount = "cracklife" )]
public sealed class ClusterGrenade : CrackLifeHandGrenade
{
	internal override void SpawnGrenade( Vector3 origin, Vector3 velocity, float fuse ) =>
		SpawnGrenade<ClusterCharge>( origin, velocity, fuse, "Cluster grenade" );
}

[Hide]
public sealed class ClusterCharge : TimedGrenade
{
	const int BombletCount = 6;

	internal override void Detonate()
	{
		var origin = Explode();
		if ( EffectsMount is not { } mount || mount.FindMountPath( "models/w_grenade.mdl" ) is not { } path ) return;

		var siblings = new List<TimedGrenade>( BombletCount );
		for ( var i = 0; i < BombletCount; i++ )
		{
			var spread = new Vector3( Game.Random.Int( -200, 200 ), Game.Random.Int( -200, 200 ), Game.Random.Int( -200, 200 ) );
			var velocity = spread * Game.Random.Int( 5, 10 );
			var fuse = Game.Random.Float( 1, 5 );
			var bomblet = Spawn<TimedGrenade>( Thrower, origin, velocity, fuse, "Cluster bomblet", $"mount://{mount.Ident}/{path}.vmdl", child =>
			{
				child.AddComponent<PhysicsFilter>().Body = GameObject;
				foreach ( var sibling in siblings )
					child.AddComponent<PhysicsFilter>().Body = sibling.GameObject;
			}, effectsMount: mount );
			siblings.Add( bomblet );
		}
	}
}

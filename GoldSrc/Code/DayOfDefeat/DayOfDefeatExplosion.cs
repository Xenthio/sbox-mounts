using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Library]
internal static class DayOfDefeatExplosion
{
	internal static void Detonate( GameObject inflictor, GameObject thrower, Vector3 origin, Vector3 contact, Vector3 normal, bool hit, float damage, float scale, bool rocket = false )
	{
		if ( rocket ) Blast.DamageInRadius( inflictor, thrower, origin, damage, damage * 2.5f, "blast", "dod_rocket_blast" );
		else Blast.DamageInRadius( inflictor, thrower, origin, damage, damage * 2.5f, "blast" );
		var underwater = WaterBody.Find( inflictor.Scene, origin ).IsValid();
		var mount = Sandbox.Mounting.Directory.Get( "dod" ) as DayOfDefeatMount;
		mount?.NpcAudio.Emit( underwater ? "weapons/explodewater.wav" : $"weapons/explode{Game.Random.Int( 3, 5 )}.wav", origin );
		Show( rocket, origin, contact, normal, hit, underwater, scale );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static async void Show( bool rocket, Vector3 origin, Vector3 contact, Vector3 normal, bool hit, bool underwater, float scale )
	{
		if ( Application.IsDedicatedServer || Sandbox.Mounting.Directory.Get( "dod" ) is not DayOfDefeatMount mount ) return;
		var scene = Game.ActiveScene;
		var name = rocket ? "rocket" : "grenade";
		var sprite = rocket
			? underwater ? "effects/adrian/water_big" : "effects/adrian/explosion_flash"
			: "zerogxplode";
		var fireball = Blast.SpriteEffect( $"DoD {name} explosion", sprite, origin, scale, 15, mount: mount );
		ExplosionParticles.Spawn( origin, fireball );
		if ( hit ) ImpactDecal.Place( contact, normal, $"scorch{Game.Random.Int( 1, 2 )}", 48, mount );
		if ( !underwater )
		{
			var sparks = Game.Random.Int( 0, 3 );
			for ( var i = 0; i < sparks; i++ ) SparkShower.Spawn( origin, hit ? normal : Vector3.Zero, mount );
		}

		await GameTask.DelaySeconds( 0.3f );
		if ( !scene.IsValid() || scene != Game.ActiveScene ) return;
		if ( underwater ) Blast.Bubbles( origin, 64, 100, $"DoD {name} bubbles", mount );
		else Blast.SpriteEffect( $"DoD {name} smoke", "steam1", origin + Vector3.Up * 20, scale, 12, additive: false, mount: mount );
	}
}

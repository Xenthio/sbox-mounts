using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

public sealed partial class HeGrenadeProjectile
{
	[Rpc.Broadcast( NetFlags.HostOnly )]
	static async void ShowExplosion( string mountId, Vector3 origin, Vector3 contact, Vector3 normal, bool hit )
	{
		if ( Application.IsDedicatedServer || Sandbox.Mounting.Directory.Get( mountId ) is not GameMount mount ) return;
		var scene = Game.ActiveScene;
		var underwater = WaterBody.Find( scene, origin ).IsValid();
		if ( hit ) ImpactDecal.Place( contact, normal, $"scorch{Game.Random.Int( 1, 2 )}", 48, mount );

		var second = new Vector3( Game.Random.Float( -64, 64 ), Game.Random.Float( -64, 64 ), Game.Random.Float( 30, 35 ) );
		ShowFireball( mount, "HE grenade fireball", "fexplo", origin + Vector3.Up * 20, 2.5f );
		ShowFireball( mount, "HE grenade secondary fireball", "eexplo", origin + second, 3 );
		var sparks = Game.Random.Int( 0, 3 );
		for ( var i = 0; i < sparks; i++ ) SparkShower.Spawn( origin, hit ? normal : Vector3.Zero, mount );

		await GameTask.DelaySeconds( 0.55f );
		if ( !scene.IsValid() || scene != Game.ActiveScene ) return;
		if ( underwater ) Blast.Bubbles( origin, 64, 100, "HE grenade bubbles", mount );
		else Blast.SpriteEffect( "HE grenade smoke", "steam1", origin + Vector3.Down * 5, Game.Random.Float( 3.5f, 4.5f ), 5, additive: false, mount: mount );
	}

	static void ShowFireball( GameMount mount, string name, string asset, Vector3 position, float scale )
	{
		Blast.SpriteEffect( name, asset, position, scale, 30, mount: mount );
		var lamp = new GameObject( false, "HE grenade flash" ) { NetworkMode = NetworkMode.Never, WorldPosition = position };
		var light = lamp.AddComponent<PointLight>();
		light.LightColor = new Color32( 250, 150, 0 );
		light.Radius = 200;
		lamp.AddComponent<TemporaryEffect>().DestroyAfterSeconds = 0.1f;
		lamp.Enabled = true;
	}
}

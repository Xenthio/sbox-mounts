namespace GoldSrc.HalfLife;

internal static partial class Blast
{
	internal static Vector3 Detonate( GameObject inflictor, GameObject owner, Vector3 contact, Vector3 normal, float damage, bool hit, GameMount mount = null )
	{
		var origin = hit ? contact + normal * ((damage - 24) * 0.6f) : contact;
		DamageInRadius( inflictor, owner, origin, damage, damage * 2.5f, "blast" );
		ShowExplosion( origin, contact, normal, damage, hit, mount?.Ident );
		mount ??= Sandbox.Mounting.Directory.Get( "hl1" ) as HalfLifeMount;
		if ( mount is not null )
		{
			mount.NpcAudio.Emit( $"weapons/explode{Game.Random.Int( 3, 5 )}.wav", origin, attenuation: 0.3f );
			mount.NpcAudio.Emit( "weapons/debris", origin, volume: 0.55f );
		}
		return origin;
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static async void ShowExplosion( Vector3 origin, Vector3 contact, Vector3 normal, float damage, bool hit, string mountIdent )
	{
		var scene = Game.ActiveScene;
		var mount = mountIdent is null ? null : Sandbox.Mounting.Directory.Get( mountIdent ) as GameMount;
		bool underwater = WaterBody.Find( scene, origin ).IsValid();
		if ( !underwater )
		{
			var count = Game.Random.Int( 0, 3 );
			for ( int i = 0; i < count; ++i ) SparkShower.Spawn( origin, hit ? normal : Vector3.Zero );
		}
		if ( hit ) ImpactDecal.Place( contact, normal, $"scorch{Game.Random.Int( 1, 2 )}", 48, mount );
		ExplosionFlash( "Grenade explosion", origin, (damage - 50) * 0.06f, underwater, mount );
		await GameTask.DelaySeconds( 0.3f );
		if ( !scene.IsValid() ) return;
		if ( underwater ) Bubbles( origin, 64, 100, "Explosion bubbles", mount );
		else SmokePuff( "Explosion smoke", origin, (damage - 50) * 0.08f, mount );
	}

	internal static void ExplosionFlash( string name, Vector3 origin, float scale, bool underwater, GameMount mount = null )
	{
		var flash = SpriteEffect( name, underwater ? "wxplo1" : "zerogxplode", origin + Vector3.Up * 10, scale, 15, mount: mount );
		if ( flash.IsValid() ) flash.GetComponent<SpriteRenderer>().Color = Color.White.WithAlpha( 180f / 255 );
		ExplosionParticles.Spawn( origin, flash );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	internal static void Smoke( Vector3 origin, float scale ) => SmokePuff( "Aircraft smoke", origin, scale );

	internal static void SmokePuff( string name, Vector3 origin, float scale, GameMount mount = null )
	{
		var shade = (byte)Game.Random.Int( 20, 35 );
		var puff = SpriteEffect( name, "steam1", origin + Vector3.Up * 20, scale, 12, additive: false, mount: mount );
		if ( !puff.IsValid() ) return;
		puff.GetComponent<SpriteRenderer>().Color = new Color32( shade, shade, shade, 255 );
		puff.AddComponent<RisingSmoke>();
	}

	internal static GameObject SpriteEffect( string name, string asset, Vector3 origin, float scale, float fps, bool additive = true, GameMount mount = null )
	{
		mount ??= Sandbox.Mounting.Directory.Get( "hl1" ) as HalfLifeMount;
		if ( mount?.GetIndexedSprite( asset ) is not { } sprite ) return null;
		var animation = sprite.Animations[0];
		var image = animation.Frames[0].Texture;
		var go = new GameObject( false, name );
		go.NetworkMode = NetworkMode.Never;
		go.WorldPosition = origin;
		var renderer = go.AddComponent<SpriteRenderer>();
		renderer.Sprite = sprite;
		renderer.Size = new Vector2( image.Width, image.Height ) * scale;
		renderer.PlaybackSpeed = fps / animation.FrameRate;
		renderer.Additive = additive;
		go.AddComponent<TemporaryEffect>().DestroyAfterSeconds = animation.Frames.Count / fps;
		go.Enabled = true;
		return go;
	}

}

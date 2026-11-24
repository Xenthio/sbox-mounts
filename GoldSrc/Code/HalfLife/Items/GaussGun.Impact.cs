namespace GoldSrc.HalfLife;

public abstract partial class ClassicGaussGun
{
	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowReflection( Vector3 point, Vector3 normal, float energy )
	{
		Glow( point + normal, 0.2f, energy / 255, energy * 0.05f );
		Sparks( point, normal, 3, 0.1f, 100, 100 );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowWallHit( Vector3 point, Vector3 normal, float energy, bool primary )
	{
		ImpactDecal.Place( point, normal, $"shot{Game.Random.Int( 1, 5 )}", 10, ContentMount );
		Glow( point + normal, 1, energy / 255, 6 );
		if ( !primary ) return;
		Glow( point + normal, 0.2f, 200f / 255, 0.3f );
		Sparks( point, normal, 8, 0.6f, 200, 100 );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowPenetration( Vector3 entry, Vector3 exit, Vector3 normal, Vector3 direction, float energy )
	{
		ImpactDecal.Place( exit, normal, $"shot{Game.Random.Int( 1, 5 )}", 10, ContentMount );
		Sparks( entry, -direction, 3, 0.1f, 100, 100 );
		Glow( exit + direction, 0.1f, energy / 255, 6 );
		Sparks( exit, -direction, (int)(energy * 0.3f), 0.1f, 40, 200 );
	}

	ParticleEffect CreateGlowParticles( string name, Vector3 position, float scale )
	{
		if ( ContentMount?.GetIndexedSprite( "hotglow" ) is not { } sprite ) return null;
		var go = new GameObject( false, name ) { NetworkMode = NetworkMode.Never, WorldPosition = position };
		var effect = go.AddComponent<ParticleEffect>();
		effect.ApplyShape = true;
		effect.InitialScale = sprite.Animations[0].Frames[0].Texture.Width * scale * 0.5f;
		effect.ApplyAlpha = true;
		var renderer = go.AddComponent<ParticleSpriteRenderer>();
		renderer.Sprite = sprite;
		renderer.Additive = true;
		go.AddComponent<TemporaryEffect>().DestroyAfterSeconds = 0;
		return effect;
	}

	static ParticleFloat Fade( float brightness, float hold ) => new()
	{
		Type = ParticleFloat.ValueType.Curve,
		CurveA = new Curve( new Curve.Frame( 0, brightness ), new Curve.Frame( hold, brightness ) { Mode = Curve.HandleMode.Linear },
			new Curve.Frame( 1, 0 ) { Mode = Curve.HandleMode.Linear } )
	};

	void Glow( Vector3 point, float scale, float brightness, float life )
	{
		if ( brightness <= 0 || CreateGlowParticles( "Gauss impact", point, scale ) is not { } glow ) return;
		glow.Lifetime = life + 1;
		glow.Alpha = Fade( brightness, life / (life + 1) );
		glow.GameObject.Enabled = true;
		glow.Emit( point, 0 );
	}

	void Sparks( Vector3 origin, Vector3 direction, int count, float life, float speed, float spread )
	{
		if ( count <= 0 || CreateGlowParticles( "Gauss sparks", origin, Game.Random.Float( 0.1f, 0.2f ) ) is not { } sparks ) return;
		sparks.Lifetime = new ParticleFloat( life + 1, life + 5 );
		sparks.Alpha = Fade( 1, (life + 2) / (life + 3) );
		sparks.Force = true;
		sparks.ForceDirection = sparks.Scene.PhysicsWorld.Gravity * 0.5f;
		sparks.Collision = true;
		sparks.CollisionIgnore = new TagSet( ["player", "npc", "trigger", "debris", "ragdoll", "playercontroller"] );
		sparks.GameObject.Enabled = true;
		for ( int i = 0; i < count; ++i )
		{
			var position = origin + direction * (i / (float)System.Math.Max( 1, count - 1 ));
			if ( sparks.Emit( position, 0 ) is { } particle )
				particle.Velocity = direction * speed + new Vector3( Game.Random.Float( -127, 128 ), Game.Random.Float( -127, 128 ), Game.Random.Float( -127, 128 ) ) * (spread / 256);
		}
	}
}

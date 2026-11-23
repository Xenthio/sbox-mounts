namespace GoldSrc.HalfLife;

internal static class StreakSplash
{
	internal static ParticleEffect Create( GameObject owner, GameMount mount )
	{
		var sparks = owner.AddComponent<ParticleEffect>();
		sparks.InitialScale = 1.5f;
		sparks.Lifetime = new ParticleFloat( 0.1f, 0.5f );
		sparks.Force = true;
		sparks.ForceDirection = Vector3.Down;
		sparks.ForceScale = 400;
		var renderer = owner.AddComponent<ParticleSpriteRenderer>();
		renderer.Sprite = mount.GetIndexedSprite( "glow01" );
		renderer.VelocityStretch = true;
		renderer.StretchTime = 0.045f;
		renderer.Additive = true;
		return sparks;
	}

	internal static void Emit( ParticleEffect sparks, Vector3 point, Vector3 velocity, Color tint, int count )
	{
		for ( int i = 0; i < count; i++ )
		{
			var particle = sparks.Emit( point, 0 );
			if ( particle is null ) continue;
			particle.Color = tint;
			particle.Velocity = velocity + new Vector3( Game.Random.Float( -400, 400 ), Game.Random.Float( -400, 400 ), Game.Random.Float( -400, 400 ) );
		}
	}
}

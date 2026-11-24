using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

internal static class ImplosionTracers
{
	internal static void Spawn( Vector3 center, float life )
	{
		var go = new GameObject( false, "Implosion tracers" ) { NetworkMode = NetworkMode.Never, WorldPosition = center };
		var particles = go.AddComponent<ParticleEffect>();
		particles.Lifetime = life;
		particles.InitialScale = 0.2f;
		var renderer = go.AddComponent<ParticleSpriteRenderer>();
		renderer.Sprite = PaletteParticle.Sprite;
		renderer.Additive = true;
		renderer.VelocityStretch = true;
		renderer.StretchTime = 0.045f;
		go.AddComponent<TemporaryEffect>().DestroyAfterSeconds = life;
		go.Enabled = true;

		var count = Game.Random.Int( 160, 255 );
		for ( int i = 0; i < count; ++i )
		{
			var offset = Vector3.Random.Normal * Game.Random.Float( 32, 255 );
			if ( particles.Emit( center + offset, 0 ) is not { } particle ) continue;
			particle.Velocity = -offset / life;
			particle.Color = new Color32( 255, 192, 128, 255 );
		}
	}
}

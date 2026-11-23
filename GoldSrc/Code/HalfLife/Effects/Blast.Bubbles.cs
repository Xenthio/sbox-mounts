namespace GoldSrc.HalfLife;

internal static partial class Blast
{
	internal static void Bubbles( Vector3 origin, float radius, int count, string name, GameMount mount = null )
	{
		var water = WaterBody.Find( Game.ActiveScene, origin );
		if ( !water.IsValid() || CreateBubbles( origin, name, mount ) is not { } effect ) return;
		for ( int i = 0; i < count; ++i )
		{
			var offset = new Vector3( Game.Random.Float( -radius, radius ), Game.Random.Float( -radius, radius ), Game.Random.Float( -radius, radius ) );
			EmitBubble( effect, water, origin + offset, 1024 );
		}
	}

	internal static void BulletBubbles( Scene scene, Vector3 start, Vector3 end ) =>
		BubbleTrail( scene, start, end, System.Math.Min( 255, (int)(start.Distance( end ) / 64) ), "Bullet bubbles" );

	internal static void BubbleTrail( Scene scene, Vector3 start, Vector3 end, int count, string name )
	{
		if ( count < 1 ) return;
		var water = WaterBody.Find( scene, start );
		if ( !water.IsValid() || !water.Contains( start + Vector3.Up * 8 ) )
		{
			water = WaterBody.Find( scene, end );
			if ( !water.IsValid() || !water.Contains( end + Vector3.Up * 8 ) ) return;
		}
		if ( CreateBubbles( start, name ) is not { } effect ) return;
		for ( int i = 0; i < count; ++i )
			EmitBubble( effect, water, Vector3.Lerp( start, end, (i + Game.Random.Float( 0, 1 )) / count ), 256 );
	}

	static void EmitBubble( ParticleEffect effect, WaterBody water, Vector3 point, float maxRise )
	{
		if ( !water.Contains( point ) ) return;
		var local = water.ToSurface( point );
		var rise = Game.Random.Float( 80, 140 );
		var depth = (water.SampleHeight( local ) - local.z) / System.MathF.Max( 0.001f, water.WorldRotation.Up.z );
		var lifetime = System.MathF.Min( depth, maxRise ) / rise - 0.1f;
		if ( lifetime <= 0 || effect.Emit( point, 0 ) is not { } bubble ) return;
		bubble.Velocity = Rotation.FromYaw( Game.Random.Float( 0, 360 ) ).Forward * 8 + Vector3.Up * rise;
		bubble.DeathTime = bubble.BornTime + lifetime;
	}

	static ParticleEffect CreateBubbles( Vector3 origin, string name, GameMount mount = null )
	{
		mount ??= Sandbox.Mounting.Directory.Get( "hl1" ) as HalfLifeMount;
		if ( mount?.GetIndexedSprite( "bubble" ) is not { } sprite ) return null;
		var go = new GameObject( false, name );
		go.NetworkMode = NetworkMode.Never;
		go.WorldPosition = origin;
		var effect = go.AddComponent<ParticleEffect>();
		effect.ApplyShape = true;
		var radius = sprite.Animations[0].Frames[0].Texture.Width * 0.5f;
		effect.InitialScale = new ParticleFloat( radius / 5, radius / 2 );
		go.AddComponent<ParticleSpriteRenderer>().Sprite = sprite;
		go.AddComponent<TemporaryEffect>().DestroyAfterSeconds = 0;
		go.Enabled = true;
		return effect;
	}
}

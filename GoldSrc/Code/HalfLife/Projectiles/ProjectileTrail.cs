namespace GoldSrc.HalfLife;

[Hide]
public sealed class ProjectileTrail : Component
{
	TrailRenderer _trail;

	internal static void Attach( GameObject projectile, GameMount mount, string name, string sprite, float life, float halfWidth, Color tint )
	{
		var go = new GameObject( projectile, false, name ) { NetworkMode = NetworkMode.Never };
		var trail = go.AddComponent<TrailRenderer>();
		go.AddComponent<ProjectileTrail>()._trail = trail;
		trail.LifeTime = life;
		trail.PointDistance = 32;
		trail.MaxPoints = 256;
		trail.Width = 2 * halfWidth;
		trail.Color = new Gradient( new Gradient.ColorFrame( 0, tint ), new Gradient.ColorFrame( 1, tint.WithAlpha( 0 ) ) );
		trail.Opaque = false;
		trail.BlendMode = BlendMode.Lighten;
		if ( sprite is not null ) trail.Texturing = TrailTextureConfig.Default with { Material = mount.GetTrailMaterial( sprite ), WorldSpace = true, UnitsPerTexture = 32 };
		go.Enabled = true;
	}

	public override void OnParentDestroy()
	{
		// TE_BEAMFOLLOW stops emitting at impact. Its existing points finish fading in world space.
		GameObject.SetParent( null, true );
		_trail.Emitting = false;
		GameObject.AddComponent<TemporaryEffect>().DestroyAfterSeconds = _trail.LifeTime;
	}
}

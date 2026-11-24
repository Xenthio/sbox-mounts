namespace GoldSrc.HalfLife;

[Hide]
public sealed class SonicRing : Component
{
	static readonly Vector3[] Circle = Enumerable.Range( 0, 49 ).Select( i => Rotation.FromYaw( i * 7.5f ).Forward ).ToArray();
	LineRenderer _line;
	Vector3 _origin;
	float _radius;
	float _lifetime;
	TimeSince _age;

	internal static void Spawn( Vector3 origin, Color tint, float radius, GameMount mount = null, float width = 16,
		string texture = "shockwave", float lifetime = 0.2f )
	{
		var go = new GameObject( false, "Sonic blast" );
		go.NetworkMode = NetworkMode.Never;
		// Vector points are supplied in world space.
		var line = go.AddComponent<LineRenderer>();
		line.UseVectorPoints = true;
		line.VectorPoints = Circle.Select( _ => origin ).ToList();
		line.Width = width;
		line.Color = tint;
		line.Additive = true;
		line.Opaque = false;
		line.CastShadows = false;
		mount ??= Sandbox.Mounting.Directory.Get( "hl1" ) as HalfLifeMount;
		if ( mount is not null )
			line.Texturing = TrailTextureConfig.Default with { Material = mount.GetTrailMaterial( texture ), WorldSpace = false };
		var ring = go.AddComponent<SonicRing>();
		ring._line = line;
		ring._origin = origin;
		ring._radius = radius;
		ring._lifetime = lifetime;
		ring._age = 0;
		go.Enabled = true;
	}

	protected override void OnUpdate()
	{
		var progress = _age / _lifetime;
		if ( progress >= 1 )
		{
			GameObject.Destroy();
			return;
		}
		for ( int i = 0; i < Circle.Length; ++i )
			_line.VectorPoints[i] = _origin + Circle[i] * (_radius * progress);
	}
}

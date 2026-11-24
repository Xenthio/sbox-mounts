namespace GoldSrc.HalfLife;

[Hide]
public sealed class GluonBeam : Component
{
	ClassicGluonGun _gun;
	LineRenderer _core, _noise;
	SpriteRenderer _flare;
	Vector2 _flareSize;
	Vector3 _end;
	TimeSince _fadeAge, _pulseAge;
	bool _stopping;

	internal static GluonBeam Start( ClassicGluonGun gun, Vector3 end )
	{
		if ( gun.ContentMount is not { } mount ) return null;
		var go = new GameObject( false, "Gluon beam" ) { NetworkMode = NetworkMode.Never };
		var beam = go.AddComponent<GluonBeam>();
		beam._gun = gun;
		beam._end = end;
		var observerSprite = gun.IsProxy ? gun.ObserverEffectSprite : null;
		var material = (observerSprite is not null ? mount.GetTrailMaterial( observerSprite ) : null) ?? mount.GetTrailMaterial( gun.BeamSprite );
		beam._core = beam.CreateLine( material, 3.5f );
		if ( gun.HasBeamNoise )
		{
			beam._noise = beam.CreateLine( material, gun.IsProxy ? 5.5f : 5 );
			if ( gun.IsProxy ) beam._noise.Color = new Color( 50f / 255, 50f / 255, 1, 100f / 255 );
		}
		else beam._core.Color = Color.White;

		var sprite = observerSprite is not null ? mount.GetIndexedSprite( observerSprite ) : null;
		if ( sprite is null && gun.ImpactSprite is { } impact ) sprite = mount.GetIndexedSprite( impact );
		if ( sprite is not null )
		{
			var flare = new GameObject( go, true, "Gluon impact" ).AddComponent<SpriteRenderer>();
			var animation = sprite.Animations[0];
			var frame = animation.Frames[0].Texture;
			beam._flareSize = new Vector2( frame.Width, frame.Height );
			flare.Sprite = sprite;
			flare.Size = beam._flareSize;
			flare.PlaybackSpeed = 8 / animation.FrameRate;
			flare.Additive = true;
			beam._flare = flare;
		}
		go.Enabled = true;
		return beam;
	}

	LineRenderer CreateLine( Material material, float width )
	{
		var line = GameObject.AddComponent<LineRenderer>();
		line.UseVectorPoints = true;
		line.VectorPoints = Enumerable.Repeat( _end, 25 ).ToList();
		line.Width = width;
		line.Color = new Color( 0.5f, 0.5f, 1.25f, 0.7f );
		line.Additive = true;
		line.Opaque = line.CastShadows = false;
		line.Texturing = TrailTextureConfig.Default with { Material = material, WorldSpace = true, UnitsPerTexture = 128 };
		return line;
	}

	internal void Aim( Vector3 end, bool showFlare )
	{
		_end = end;
		_pulseAge = 0;
		if ( !_flare.IsValid() ) return;
		_flare.WorldPosition = end;
		_flare.Enabled = showFlare;
	}

	internal void Stop()
	{
		if ( _stopping ) return;
		_stopping = true;
		_fadeAge = 0;
		_core.Enabled = false;
		if ( _noise.IsValid() ) _noise.Enabled = false;
		if ( !_flare.IsValid() || !_flare.Enabled ) GameObject.Destroy();
	}

	protected override void OnUpdate()
	{
		if ( !_gun.IsValid() && !_stopping ) Stop();
		if ( _stopping )
		{
			var alpha = 1 - (float)_fadeAge * (500f / 255);
			if ( alpha <= 0 || !_flare.IsValid() ) { GameObject.Destroy(); return; }
			_flare.Size = _flareSize * (1 + (float)_fadeAge * 10);
			_flare.Color = Color.White.WithAlpha( alpha );
			return;
		}

		var start = _gun.GetMuzzleTransform().Position;
		var basis = Rotation.LookAt( _end - start );
		// Half-Life predicts fixed beams for the firing player and pulses the replicated core for observers.
		if ( _gun.IsProxy && _noise.IsValid() )
		{
			var pulse = System.Math.Clamp( (float)_pulseAge * 10, 0, 1 );
			_core.Width = 4 - 2 * pulse;
			_core.Color = new Color( (30 + 25 * pulse) / 255, (30 + 30 * pulse) / 255,
				(64 + 80 * System.MathF.Abs( System.MathF.Sin( Time.Now * 10 ) )) / 255, 1 - 180 * pulse / 255 );
		}
		UpdateLine( _core, start, basis, 0.2f, _gun.IsProxy ? 50 : 55 );
		if ( _noise.IsValid() ) UpdateLine( _noise, start, basis, 0.08f, 25 );
	}

	void UpdateLine( LineRenderer line, Vector3 start, Rotation basis, float amplitude, float scroll )
	{
		var points = line.VectorPoints;
		points[0] = start;
		points[^1] = _end;
		var swing = System.MathF.Min( start.Distance( _end ), 256 ) * amplitude * 0.25f;
		for ( int i = 1; i < points.Count - 1; i++ )
		{
			var t = (float)i / (points.Count - 1);
			var phase = t * 18 + Time.Now * scroll;
			var offset = basis.Right * System.MathF.Sin( phase ) + basis.Up * System.MathF.Cos( phase );
			points[i] = Vector3.Lerp( start, _end, t ) + offset * (swing * System.MathF.Sin( t * System.MathF.PI ));
		}
		line.Texturing = line.Texturing with { Offset = Time.Now * scroll / 128 };
	}
}

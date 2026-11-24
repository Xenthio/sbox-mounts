namespace GoldSrc.ScientistSlaughterhouse;

[Hide]
public sealed class IonDischargeBeam : Component
{
	readonly List<Stream> _streams = [];
	GluonGun _weapon;
	Material _material;
	Sprite _sprite;
	Vector2 _spriteSize;
	Vector3[] _endpoints = [];

	sealed record Stream( LineRenderer Line, SpriteRenderer Muzzle, SpriteRenderer Impact )
	{
		internal void SetEnabled( bool enabled )
		{
			Line.Enabled = enabled;
			if ( Muzzle.IsValid() ) Muzzle.Enabled = enabled;
			if ( Impact.IsValid() ) Impact.Enabled = enabled;
		}
	}

	internal static IonDischargeBeam Start( GluonGun weapon )
	{
		if ( weapon.ContentMount is not { } mount ) return null;
		var visual = new GameObject( false, "Ion discharge beams" ) { NetworkMode = NetworkMode.Never };
		var effect = visual.AddComponent<IonDischargeBeam>();
		effect._weapon = weapon;
		effect._material = mount.GetTrailMaterial( "ionstream3" );
		effect._sprite = mount.GetIndexedSprite( "thunderball" );
		if ( effect._sprite is { } sprite )
		{
			var image = sprite.Animations[0].Frames[0].Texture;
			effect._spriteSize = new Vector2( image.Width, image.Height );
		}
		visual.Enabled = true;
		return effect;
	}

	internal void Aim( Vector3[] endpoints )
	{
		_endpoints = endpoints;
		while ( _streams.Count < endpoints.Length ) _streams.Add( CreateStream() );
		for ( int i = 0; i < _streams.Count; i++ ) _streams[i].SetEnabled( i < endpoints.Length );
		UpdateStreams();
	}

	Stream CreateStream()
	{
		var line = GameObject.AddComponent<LineRenderer>();
		line.UseVectorPoints = true;
		line.VectorPoints = [Vector3.Zero, Vector3.Zero];
		line.Width = 2.4f;
		line.Color = Color.White;
		line.Additive = true;
		line.Opaque = line.CastShadows = false;
		line.Texturing = TrailTextureConfig.Default with { Material = _material, WorldSpace = true, UnitsPerTexture = 128 };
		return new Stream( line, CreateGlow( 0.2f ), CreateGlow( 0.5f ) );
	}

	SpriteRenderer CreateGlow( float scale )
	{
		if ( _sprite is null ) return null;
		var renderer = new GameObject( GameObject, true, "Ion stream glow" ).AddComponent<SpriteRenderer>();
		renderer.Sprite = _sprite;
		renderer.PlaybackSpeed = 14 / _sprite.Animations[0].FrameRate;
		renderer.Additive = true;
		renderer.Size = _spriteSize * scale;
		return renderer;
	}

	protected override void OnUpdate()
	{
		if ( !_weapon.IsValid() || !_weapon.IsHeld ) GameObject.Destroy();
		else UpdateStreams();
	}

	void UpdateStreams()
	{
		var start = _weapon.GetMuzzleTransform().Position;
		for ( var i = 0; i < _endpoints.Length; i++ )
		{
			var stream = _streams[i];
			stream.Line.VectorPoints[0] = start;
			stream.Line.VectorPoints[1] = _endpoints[i];
			if ( stream.Muzzle.IsValid() ) stream.Muzzle.WorldPosition = start;
			if ( stream.Impact.IsValid() ) stream.Impact.WorldPosition = _endpoints[i];
		}
	}
}

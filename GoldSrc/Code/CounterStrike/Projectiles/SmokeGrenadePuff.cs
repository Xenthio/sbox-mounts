namespace GoldSrc.CounterStrike;

[Hide]
public sealed class SmokeGrenadePuff : Component
{
	const float CloudHold = 15;
	const float CloudFade = 255f / 18;
	TimeSince _age;
	float _hold, _fade, _driftTime;
	Vector3 _origin, _velocity;
	Color _tint;
	SpriteRenderer _renderer;

	internal static void ShowCloud( GameMount mount, Vector3 origin, int seed, float age )
	{
		if ( age >= CloudHold + CloudFade || mount.GetIndexedSprite( "gas_puff_01" ) is not { } sprite ) return;
		var random = new System.Random( seed );
		for ( var i = 0; i < 20; i++ )
		{
			var offset = i == 0 ? Vector3.Zero : new Vector3( random.Next( -100, 101 ), random.Next( -100, 101 ), 0 );
			var start = origin + Vector3.Up * 30;
			var end = start + offset;
			var cover = Game.ActiveScene.Trace.Ray( start, end ).IgnoreDynamic().WithoutTags( "trigger" ).Run();
			if ( cover.Hit ) end = cover.EndPosition + cover.Normal * 2;
			var drift = new Vector3( random.Next( -5, 6 ), random.Next( -5, 6 ), 0 );
			var shade = random.Next( 210, 231 ) / 255f;
			Spawn( "Smoke grenade cloud puff", sprite, end, drift, 5, new Color( shade, shade, shade ), CloudHold, CloudFade, age );
		}
	}

	internal static void ShowVent( GameMount mount, Vector3 origin, Vector3 direction )
	{
		if ( mount.GetIndexedSprite( $"black_smoke{Game.Random.Int( 1, 4 )}" ) is not { } sprite ) return;
		var speed = Game.Random.Float( 3, 8 );
		var shade = Game.Random.Float( 155, 175 ) / 255f;
		Spawn( "Smoke grenade vent puff", sprite, origin, direction * speed + Vector3.Up * 25,
			1, new Color( shade, shade, shade ), 0.3f, 1.4f, 0 );
	}

	static void Spawn( string name, Sprite sprite, Vector3 position, Vector3 velocity, float scale, Color tint, float hold, float fade, float age )
	{
		var go = new GameObject( false, name ) { NetworkMode = NetworkMode.Never };
		var puff = go.AddComponent<SmokeGrenadePuff>();
		var image = sprite.Animations[0].Frames[0].Texture;
		puff._renderer = go.AddComponent<SpriteRenderer>();
		puff._renderer.Sprite = sprite;
		puff._renderer.Size = new Vector2( image.Width, image.Height ) * scale;
		puff._renderer.Color = tint;
		puff._origin = position;
		puff._velocity = velocity;
		puff._tint = tint;
		puff._hold = hold;
		puff._fade = fade;
		puff._age = age;
		go.WorldPosition = position;
		var travelTime = hold + fade;
		var path = go.Scene.Trace.Ray( position, position + velocity * travelTime ).IgnoreDynamic().WithoutTags( "trigger" ).Run();
		puff._driftTime = travelTime * path.Fraction;
		go.Enabled = true;
		puff.UpdatePuff();
	}

	protected override void OnUpdate() => UpdatePuff();

	void UpdatePuff()
	{
		float age = _age;
		if ( age >= _hold + _fade )
		{
			GameObject.Destroy();
			return;
		}
		WorldPosition = _origin + _velocity * System.MathF.Min( age, _driftTime );
		_renderer.Color = _tint.WithAlpha( ((_hold + _fade - age) / _fade).Clamp( 0, 1 ) );
	}
}

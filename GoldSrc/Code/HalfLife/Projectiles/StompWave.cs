namespace GoldSrc.HalfLife;

[Hide]
public sealed class StompWave : Component
{
	[Property] public GameObject Attacker { get; set; }
	[Property] public Vector3 End { get; set; }
	[Property] public float Damage { get; set; } = 100;
	[Property] public float EffectScale { get; set; } = 1;
	[Property] public string MountIdent { get; set; } = "hl1";
	Vector3 _direction;
	float _speed, _acceleration = 30;
	ParticleEffect _dust;
	TimeUntil _emit, _nextHit;
	Vector3 _lastTrace;
	SoundHandle _buzz;

	internal static void Spawn( GargantuaNpc source, Vector3 start, Vector3 end, float damage, float scale )
	{
		var go = new GameObject( false, "Stomp wave" );
		go.WorldPosition = start;
		var wave = go.AddComponent<StompWave>();
		wave.Attacker = source.GameObject;
		wave.End = end;
		wave.Damage = damage;
		wave.EffectScale = scale;
		wave.MountIdent = source.Definition.Mount;
		go.Enabled = true;
		go.NetworkSpawn( true, null );
	}

	protected override void OnStart()
	{
		_direction = (End - WorldPosition).Normal;
		_lastTrace = WorldPosition;
		if ( Sandbox.Mounting.Directory.Get( MountIdent ) is not GameMount mount ) return;
		if ( mount.GetIndexedSprite( "gargeye1" ) is { } sprite )
		{
			_dust = GameObject.AddComponent<ParticleEffect>();
			_dust.Lifetime = 0.3f;
			_dust.InitialScale = 16 * EffectScale;
			var renderer = GameObject.AddComponent<ParticleSpriteRenderer>();
			renderer.Sprite = sprite;
			renderer.Additive = true;
		}
		if ( mount.NpcAudio.Find( "weapons/mine_charge.wav" ) is { } path && SoundFile.Load( path ) is { } sound )
		{
			_buzz = GoldSrcSoundEvents.Play( sound, WorldPosition, pitch: 0.55f );
		}
	}

	protected override void OnUpdate()
	{
		if ( _buzz.IsValid() ) _buzz.Position = WorldPosition;
		if ( !_dust.IsValid() || _emit > 0 ) return;
		_emit = 0.025f;
		var trace = Scene.Trace.Ray( WorldPosition, WorldPosition + Vector3.Down * 500 )
			.WithoutTags( "npc", "player", "playercontroller", "trigger" ).Run();
		for ( int i = 0; i < 2; ++i )
		{
			var sprite = _dust.Emit( trace.EndPosition, 0 );
			if ( sprite is not null ) sprite.Velocity = new Vector3( Game.Random.Float( -200, 200 ), Game.Random.Float( -200, 200 ), 175 );
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;
		var remaining = WorldPosition.Distance( End );
		_speed += _acceleration * Time.Delta;
		_acceleration += 1500 * Time.Delta;
		var distance = System.MathF.Min( remaining, _speed * Time.Delta );
		WorldPosition += _direction * distance;
		if ( _nextHit <= 0 || distance >= remaining )
		{
			Sweep( _lastTrace );
			_lastTrace = WorldPosition;
			_nextHit = 0.1f;
		}
		if ( distance >= remaining ) GameObject.Destroy();
	}

	void Sweep( Vector3 start )
	{
		var trace = Scene.Trace.Ray( start + Vector3.Up * (30 * EffectScale), WorldPosition + Vector3.Up * (30 * EffectScale) ).Size( 32 * EffectScale );
		if ( Attacker.IsValid() ) trace = trace.IgnoreGameObjectHierarchy( Attacker );
		var hit = trace.Run();
		if ( hit.GameObject?.GetComponentInParent<IDamageable>() is { } victim )
		{
			var damage = new DamageInfo { Damage = Damage, Origin = start, Position = hit.EndPosition, Attacker = Attacker, Weapon = GameObject };
			damage.Tags.Add( "sonic" );
			victim.OnDamage( damage );
		}
	}

	protected override void OnDisabled() => _buzz?.Stop();
}

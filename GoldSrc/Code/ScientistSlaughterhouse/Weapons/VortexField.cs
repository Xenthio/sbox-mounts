using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

[Hide]
public sealed class VortexField : Component
{
	const float GrowthTime = 2;
	const float FadeStart = GrowthTime + 25;
	const float FadeTime = 255f / 200;
	[Property] public GameObject Shooter { get; set; }
	[Property] public bool BlackHole { get; set; }
	[Property] public float Damage { get; set; }
	[Sync( SyncFlags.FromHost )] TimeSince Age { get; set; }
	GameMount Mount => Sandbox.Mounting.Directory.Get( "ssh" ) as GameMount;
	TimeUntil _nextPull, _nextArc, _nextShake;
	SpriteRenderer _sprite;
	SoundHandle _wind;
	bool _windStarted;

	internal static void Spawn( GameObject shooter, Vector3 position, float damage, bool blackHole )
	{
		var go = new GameObject( false, blackHole ? "Black hole" : "Vortex blast" ) { WorldPosition = position };
		var field = go.AddComponent<VortexField>();
		field.Shooter = shooter;
		field.BlackHole = blackHole;
		field.Damage = damage;
		field.Age = 0;
		go.Enabled = true;
		go.NetworkSpawn( true, null );
	}

	protected override void OnStart()
	{
		if ( !Application.IsDedicatedServer && Mount is { } mount )
		{
			if ( BlackHole && mount.GetIndexedSprite( "blackhole" ) is { } sprite )
			{
				_sprite = GameObject.AddComponent<SpriteRenderer>();
				_sprite.Sprite = sprite;
				_sprite.Size = Vector2.Zero;
				_sprite.Color = Color.White.WithAlpha( 0 );
				NpcAudio.Play( mount.NpcAudio.Find( "ambience/particle_suck2.wav" ), WorldPosition );
			}
			else if ( !BlackHole )
				Blast.SpriteEffect( "Vortex implosion", "c-tele1", WorldPosition, 1, 20, mount: mount );
			if ( !BlackHole ) ImplosionTracers.Spawn( WorldPosition, 1.2f );
		}
	}

	protected override void OnUpdate()
	{
		if ( !BlackHole || Application.IsDedicatedServer || Mount is not { } mount ) return;
		var scale = ((float)Age / GrowthTime).Clamp( 0, 1 );
		if ( _sprite.IsValid() )
		{
			_sprite.Size = Vector2.One * (128 * scale);
			var fade = (1 - ((float)Age - FadeStart) / FadeTime).Clamp( 0, 1 );
			_sprite.Color = Color.White.WithAlpha( scale * fade );
		}
		if ( Age >= FadeStart )
			_wind?.Stop();
		else if ( Age >= GrowthTime && !_windStarted )
		{
			_windStarted = true;
			_wind = NpcAudio.Play( mount.NpcAudio.Find( "ambience/alienwind1.wav" ), WorldPosition );
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;
		if ( !BlackHole )
		{
			if ( Age < 1.2f ) return;
			ShowRing( WorldPosition, Damage * 5 );
			Blast.Detonate( GameObject, Shooter, WorldPosition, Vector3.Up, Damage, false, Mount );
			GameObject.Destroy();
			return;
		}

		if ( Age >= FadeStart )
		{
			if ( Age >= FadeStart + FadeTime ) GameObject.Destroy();
			return;
		}

		if ( Age < GrowthTime || _nextPull > 0 ) return;
		_nextPull = 0.1f;
		PullVictims();
		if ( _nextShake <= 0 )
		{
			_nextShake = 1;
			GroundShake.Emit( Scene, WorldPosition, 500, 0.2f, 2, 768 );
			ShowPull( WorldPosition );
		}
		if ( _nextArc <= 0 )
		{
			_nextArc = Game.Random.Float( 0.2f, 2 );
			ShowArc( WorldPosition, WorldPosition + Vector3.Random * Game.Random.Float( 512, 1024 ) );
		}
	}

	void PullVictims()
	{
		var victims = Scene.FindInPhysics( new Sphere( WorldPosition, 512 ) )
			.Select( obj => obj.GetComponentInParent<IDamageable>() ).OfType<Component>().Distinct().ToArray();
		foreach ( var victim in victims )
		{
			if ( !victim.IsValid() || victim.GetComponentInChildren<BaseInventoryComponent>()?.ActiveItem is VortexLauncher ) continue;
			var offset = WorldPosition - victim.WorldPosition;
			var impulse = (offset.IsNearlyZero() ? Vector3.Up : offset.Normal) * 140;
			if ( victim.GetComponent<Rigidbody>() is { } body )
			{
				if ( victim is Npc npc && npc.Definition?.Movement != NpcMovement.Stationary )
					npc.Launch( body.Velocity + impulse );
				else if ( body.MotionEnabled )
					body.Velocity += impulse;
			}
			if ( offset.LengthSquared >= 256 * 256 ) continue;
			var damage = new DamageInfo { Damage = offset.LengthSquared < 8 * 8 ? 300 : 30, Attacker = Shooter,
				Weapon = GameObject, Origin = WorldPosition, Position = victim.WorldPosition };
			damage.Tags.Add( offset.LengthSquared < 8 * 8 ? "alwaysgib" : "blast" );
			((IDamageable)victim).OnDamage( damage );
		}
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowRing( Vector3 position, float radius ) =>
		SonicRing.Spawn( position, new Color32( 255, 192, 128, 128 ), radius, Mount, width: 3.2f, texture: "lgtning", lifetime: 0.4f );

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowArc( Vector3 start, Vector3 end ) =>
		EnergyBeam.Show( start, end, new Color32( 255, 192, 128, 128 ), "obeam1", "Black hole lightning", 2, 0.2f, Mount );

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowPull( Vector3 position )
	{
		if ( !Application.IsDedicatedServer ) ImplosionTracers.Spawn( position, 1 );
	}

	protected override void OnDestroy() => _wind?.Stop();
}

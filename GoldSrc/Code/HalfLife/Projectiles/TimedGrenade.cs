namespace GoldSrc.HalfLife;

[Hide]
public class TimedGrenade : Component, Component.ICollisionListener
{
	[Property] public GameObject Thrower { get; set; }
	GameMount _effectsMount;
	TimeUntil _fuse, _nextBounce, _nextImpact, _nextTumble;
	bool _settledWarning;
	protected float FuseRemaining => _fuse;
	internal virtual float GravityScale => 0.5f;
	internal virtual float Friction => 0.8f;
	internal virtual bool DealsImpactDamage => true;
	internal const string DangerStimulus = "grenade";

	internal static bool TryAim( Npc owner, Vector3 origin, GameObject target, Vector3 end, out Vector3 velocity, float flightTime = 1, bool requireFloor = true )
	{
		var scene = owner.Scene;
		var gravity = -scene.PhysicsWorld.Gravity.z * 0.5f;
		// Use the same flight time and gravity for the launch velocity and the clearance trace.
		velocity = (end - origin) / flightTime + Vector3.Up * (gravity * 0.5f * flightTime);
		var ground = scene.Trace.Ray( target.WorldPosition + Vector3.Up * 8, target.WorldPosition + Vector3.Down * 12 )
			.IgnoreGameObjectHierarchy( target ).Run();
		if ( requireFloor && !ground.Hit ) return false;
		var previous = origin;
		for ( int i = 1; i <= 8; i++ )
		{
			var t = i * flightTime / 8f;
			var next = origin + velocity * t + Vector3.Down * (gravity * 0.5f * t * t);
			if ( scene.Trace.Sphere( 2, previous, next ).IgnoreGameObjectHierarchy( owner.GameObject ).IgnoreGameObjectHierarchy( target ).Run().Hit )
				return false;
			previous = next;
		}
		return true;
	}

	internal static void Throw( Npc owner, Vector3 origin, Vector3 velocity, float fuse ) => Throw( owner.GameObject, origin, velocity, fuse );

	internal static void Throw( GameObject owner, Vector3 origin, Vector3 velocity, float fuse ) =>
		Spawn<TimedGrenade>( owner, origin, velocity, fuse, "Hand grenade", "mount://hl1/valve/models/w_grenade.mdl.vmdl" );

	internal static T Spawn<T>( GameObject owner, Vector3 origin, Vector3 velocity, float fuse, string name, string modelPath,
		System.Action<T> configure = null, GameMount effectsMount = null )
		where T : TimedGrenade, new()
	{
		var go = new GameObject( false, name );
		go.WorldPosition = origin;
		if ( !velocity.IsNearlyZero() ) go.WorldRotation = Rotation.LookAt( velocity );
		var model = Model.Load( modelPath );
		go.AddComponent<ModelRenderer>().Model = model;
		var grenade = go.AddComponent<T>();
		configure?.Invoke( grenade );
		var collider = go.AddComponent<ModelCollider>();
		collider.Model = model;
		collider.Elasticity = 0.45f;
		collider.Friction = grenade.Friction;
		var body = go.AddComponent<Rigidbody>();
		body.EnhancedCcd = true;
		body.GravityScale = grenade.GravityScale;
		body.EnableImpactDamage = false;
		grenade.Thrower = owner;
		grenade._effectsMount = effectsMount;
		grenade._fuse = fuse;
		grenade._nextTumble = 0.1f;
		body.RigidbodyFlags |= RigidbodyFlags.DisableCollisionSounds;
		go.Enabled = true;
		body.Velocity = fuse < 0.1f ? Vector3.Zero : velocity;
		if ( fuse >= 0.1f ) body.AngularVelocity = go.WorldRotation.Right * Game.Random.Float( 8, 14 );
		go.NetworkSpawn( true, null );
		return grenade;
	}

	void ICollisionListener.OnCollisionStart( Collision collision )
	{
		if ( !Networking.IsHost || collision.Other.IsTrigger ) return;
		var other = collision.Other.GameObject;
		if ( other.IsValid() && Thrower.IsValid() && other.Root == Thrower.Root ) return;
		var body = GetComponent<Rigidbody>();
		if ( !body.IsValid() ) return;
		var velocity = body.Velocity;
		if ( DealsImpactDamage && _nextImpact <= 0 && collision.Contact.Speed.Length > 100 )
		{
			_nextImpact = 1;
			if ( other.IsValid() && Thrower.IsValid() && other.GetComponentInParent<IDamageable>() is { } victim )
			{
				var hit = new DamageInfo { Damage = 1, Attacker = Thrower, Weapon = GameObject, Origin = WorldPosition, Position = collision.Contact.Point };
				hit.Tags.Add( "club" );
				victim.OnDamage( hit );
			}
		}
		if ( !_settledWarning && velocity.WithZ( velocity.z * 0.45f ).Length <= 60 )
		{
			_settledWarning = true;
			Scene.GetSystem<Sandbox.Npcs.NpcSystem>().EmitStimulus( DangerStimulus, WorldPosition, GameObject, 250, 0.3f );
		}
		var rolling = System.MathF.Abs( collision.Contact.Normal.z ) > 0.7f && System.MathF.Abs( velocity.z ) < 30;
		if ( rolling ) body.Velocity *= 0.8f;
		if ( rolling || _nextBounce > 0 || System.MathF.Abs( collision.Contact.NormalSpeed ) < 10 ) return;
		_nextBounce = 0.12f;
		OnBounce( body );
	}

	internal virtual void OnBounce( Rigidbody body )
	{
		var mount = _effectsMount ?? Sandbox.Mounting.Directory.Get( "hl1" ) as HalfLifeMount;
		mount?.NpcAudio.Emit( "weapons/grenade_hit", WorldPosition, volume: 0.25f );
	}

	internal virtual bool ReadyToDetonate( Rigidbody body ) => true;
	internal virtual void OnTumble( Rigidbody body ) { }

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;
		var body = GetComponent<Rigidbody>();
		if ( _fuse <= 0 && ReadyToDetonate( body ) )
		{
			OnFuseExpired();
			return;
		}
		if ( _nextTumble > 0 ) return;
		_nextTumble = 0.1f;
		if ( _fuse > 0 && _fuse < 1 )
		{
			var landing = WorldPosition + body.Velocity * (float)_fuse;
			Scene.GetSystem<Sandbox.Npcs.NpcSystem>().EmitStimulus( DangerStimulus, landing, GameObject, 400, 0.1f );
		}
		if ( WaterBody.Find( Scene, WorldPosition ).IsValid() )
		{
			body.Velocity *= 0.5f;
			body.AngularVelocity *= 0.5f;
		}
		OnTumble( body );
	}

	internal virtual void OnFuseExpired()
	{
		Detonate();
		GameObject.Destroy();
	}

	internal virtual void Detonate()
	{
		var floor = Scene.Trace.Ray( WorldPosition + Vector3.Up * 8, WorldPosition + Vector3.Down * 40 )
			.IgnoreGameObjectHierarchy( GameObject ).Run();
		Blast.Detonate( GameObject, Thrower, floor.Hit ? floor.EndPosition : WorldPosition, floor.Normal, 100, floor.Hit, _effectsMount );
	}
}

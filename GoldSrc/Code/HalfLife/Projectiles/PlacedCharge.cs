namespace GoldSrc.HalfLife;

[Hide]
public sealed partial class PlacedCharge : Component, Component.IDamageable, Component.IDamageable.IEvents, Component.ICollisionListener
{
	[Property] public GameObject Thrower { get; set; }
	[Property] public bool IsMine { get; set; }
	[Property] public string EffectsMountId { get; set; }
	internal GameMount EffectsMount => EffectsMountId is null ? null : Sandbox.Mounting.Directory.Get( EffectsMountId ) as GameMount;
	float? _damageOverride;
	bool _spent;
	float _satchelGravity = 0.5f;
	TimeUntil _nextWaterCheck;

	void Component.IDamageable.IEvents.OnDied( Component.IDamageable.DeathEvent death )
	{
		if ( !Networking.IsHost || Networking.MaxPlayers <= 1 || IsMine || death.Victim != Thrower ) return;
		if ( Thrower.GetComponentInChildren<ClassicSatchel>( true ).IsValid() ) GameObject.Destroy();
	}

	internal static void Throw( PlayerController player, Ray ray ) => SpawnSatchel( player, ray );

	internal static bool SpawnSatchel( PlayerController player, Ray ray, GameMount mount = null, float? damage = null )
	{
		var path = mount is null ? "valve/models/w_satchel.mdl" : mount.FindMountPath( "models/w_satchel.mdl" );
		if ( path is null ) return false;
		var model = Model.Load( $"mount://{mount?.Ident ?? "hl1"}/{path}.vmdl" );
		if ( !model.IsValid() || model.IsError ) return false;
		var go = new GameObject( false, "Satchel charge" );
		go.WorldPosition = ray.Position;
		go.WorldRotation = Rotation.LookAt( ray.Forward );
		var renderer = go.AddComponent<SkinnedModelRenderer>();
		renderer.Model = model;
		renderer.UseAnimGraph = false;
		renderer.Sequence.Name = "onback";
		var collider = go.AddComponent<BoxCollider>();
		collider.Scale = new Vector3( 8, 8, 8 );
		collider.Friction = 0.8f;
		var body = go.AddComponent<Rigidbody>();
		body.GravityScale = 0.5f;
		body.EnableImpactDamage = false;
		body.RigidbodyFlags |= RigidbodyFlags.DisableCollisionSounds;
		var charge = go.AddComponent<PlacedCharge>();
		charge.Thrower = player.GameObject;
		charge.EffectsMountId = mount?.Ident;
		charge._damageOverride = damage;
		if ( mount is not null && player.Body.IsValid() ) go.AddComponent<PhysicsFilter>().Body = player.Body.GameObject;
		go.Enabled = true;
		body.Velocity = ray.Forward * 274 + player.Velocity;
		body.AngularVelocity = Vector3.Up * 400f.DegreeToRadian();
		go.NetworkSpawn( true, null );
		return true;
	}

	void ICollisionListener.OnCollisionStart( Collision collision )
	{
		if ( !Networking.IsHost || IsMine || collision.Other.IsTrigger || collision.Other.GameObject == Thrower ) return;
		var body = GetComponent<Rigidbody>();
		_satchelGravity = 1;
		if ( body.GravityScale > 0 ) body.GravityScale = 1;
		if ( collision.Contact.Speed.WithZ( 0 ).Length <= 10 ) return;
		var mount = EffectsMount ?? Sandbox.Mounting.Directory.Get( "hl1" ) as HalfLifeMount;
		mount?.NpcAudio.Emit( "weapons/g_bounce", collision.Contact.Point );
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;
		if ( IsMine ) { CheckMine(); return; }
		if ( _nextWaterCheck > 0 ) return;
		_nextWaterCheck = 0.1f;
		var body = GetComponent<Rigidbody>();
		if ( WaterBody.Find( Scene, WorldPosition + Vector3.Up * 4 ).IsValid() )
		{
			body.GravityScale = 0;
			body.Velocity = body.Velocity * 0.8f + Vector3.Up * 8;
			body.AngularVelocity *= 0.9f;
		}
		else
		{
			body.GravityScale = _satchelGravity;
			if ( WaterBody.Find( Scene, WorldPosition - Vector3.Up * 4 ).IsValid() )
				body.Velocity += Vector3.Down * 8;
		}
	}

	void Component.IDamageable.OnDamage( in DamageInfo damage )
	{
		if ( !Networking.IsHost || !IsMine || _spent || _detonateAt.HasValue || damage.Damage <= 0 ) return;

		if ( _arm > 0 && damage.Damage < _mineHealth )
		{
			_disarmed = true;
			_detonateAt = 0.1f;
			StopChargeSound();
			return;
		}
		_mineHealth -= damage.Damage;
		if ( _mineHealth > 0 ) return;
		if ( damage.Attacker?.GetComponentInParent<PlayerController>() is { } attacker ) Thrower = attacker.GameObject;
		TriggerMine();
	}

	void TriggerMine()
	{
		StopChargeSound();
		_detonateAt = Game.Random.Float( 0.1f, 0.3f );
	}

	internal void Detonate()
	{
		if ( _spent ) return;
		_spent = true;
		var normal = IsMine ? WorldRotation.Forward : Vector3.Up;
		var contact = Scene.Trace.Ray( WorldPosition + normal * 8, WorldPosition - normal * (IsMine ? 64 : 40) )
			.IgnoreGameObjectHierarchy( GameObject ).Run();
		var damage = _damageOverride ?? (!IsMine && Networking.MaxPlayers > 1 ? 120 : 150);
		Blast.Detonate( GameObject, Thrower, contact.Hit ? contact.EndPosition : WorldPosition, contact.Hit ? contact.Normal : normal, damage, contact.Hit, EffectsMount );
		GameObject.Destroy();
	}
}

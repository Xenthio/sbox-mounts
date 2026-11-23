using GoldSrc.HalfLife;

namespace GoldSrc.TeamFortressClassic;

[Hide]
public sealed class FortressGrenade : Component, Component.IDamageable.IEvents
{
	const int PipeLimit = 8;
	const float Damage = 120;
	const float TimedFuse = 2.5f;
	const float PipeFuse = 120f;
	const float ArmDelay = 0.6f;

	[Property] public GameObject Shooter { get; set; }
	[Property] public bool IsPipe { get; set; }
	TimeSince _age = 0;
	Vector3 _velocity;
	bool _settled, _spent;

	internal bool IsArmed => _age >= ArmDelay && !_spent;
	static GameMount Mount => Sandbox.Mounting.Directory.Get( "tfc" ) as GameMount;

	internal static IEnumerable<FortressGrenade> PipesOwnedBy( GameObject shooter ) => shooter.Scene.GetAllComponents<FortressGrenade>()
		.Where( projectile => projectile.IsPipe && !projectile._spent && projectile.Shooter == shooter );

	internal static void Launch( GameObject shooter, Vector3 origin, Rotation rotation, Vector3 velocity, bool pipe, string modelPath )
	{
		if ( pipe )
		{
			var previous = PipesOwnedBy( shooter ).OrderByDescending( charge => (float)charge._age ).ToArray();
			if ( previous.Length >= PipeLimit ) previous[0].Detonate();
		}
		var go = new GameObject( false, pipe ? "TFC pipebomb" : "TFC grenade" );
		go.WorldTransform = new Transform( origin, rotation );
		go.Flags |= GameObjectFlags.NotSaved;
		var renderer = go.AddComponent<ModelRenderer>();
		renderer.Model = Model.Load( modelPath );
		if ( !pipe ) renderer.MaterialGroup = "Skin 1";
		var projectile = go.AddComponent<FortressGrenade>();
		projectile.Shooter = shooter;
		projectile.IsPipe = pipe;
		projectile._velocity = velocity;
		go.Enabled = true;
		go.NetworkSpawn( true, null );
	}

	protected override void OnStart()
	{
		if ( !Application.IsDedicatedServer && Mount is { } mount )
			ProjectileTrail.Attach( GameObject, mount, "TFC grenade trail", "smoke", 1, 5, new Color32( 224, 224, 255, 80 ) );
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost || _spent ) return;
		if ( IsPipe && !Shooter.IsValid() ) { GameObject.Destroy(); return; }
		if ( _age >= (IsPipe ? PipeFuse : TimedFuse) ) { Detonate(); return; }
		if ( _settled ) return;

		var gravity = Scene.PhysicsWorld.Gravity;
		var remaining = Time.Delta;
		for ( var contacts = 0; contacts < 4 && remaining > 0 && !_settled; contacts++ )
		{
			var start = WorldPosition;
			var end = start + (_velocity + gravity * (remaining * 0.5f)) * remaining;
			var trace = Scene.Trace.Ray( start, end ).IgnoreGameObjectHierarchy( GameObject ).IgnoreGameObjectHierarchy( Shooter )
				.WithoutTags( "trigger", "debris", "playerclip", "playercontroller" ).UseHitboxes().Run();
			var travel = remaining * (trace.Hit ? trace.Fraction : 1);
			_velocity += gravity * travel;
			WorldRotation *= Rotation.From( 300 * travel, 300 * travel, 300 * travel );
			WorldPosition = trace.EndPosition;
			if ( !trace.Hit ) return;
			if ( !IsPipe && trace.GameObject.IsValid() && (trace.GameObject.GetComponentInParent<PlayerController>().IsValid() ||
				trace.GameObject.GetComponentInParent<Sandbox.Npcs.BaseNpc>().IsValid()) )
			{
				Detonate( trace.Normal );
				return;
			}
			if ( IsPipe && _velocity.Length > 20 ) Mount?.NpcAudio.Emit( "weapons/grenade_hit", WorldPosition, volume: 0.25f );
			WorldPosition += trace.Normal * 0.125f;
			_velocity -= trace.Normal * (1.5f * System.MathF.Min( 0, _velocity.Dot( trace.Normal ) ));
			if ( trace.Normal.z > 0.7f )
			{
				_velocity *= IsPipe ? 0.8f : 0.75f;
				_settled = _velocity.Length < 20;
			}
			remaining -= travel;
		}
	}

	void Component.IDamageable.IEvents.OnDied( Component.IDamageable.DeathEvent death )
	{
		if ( Networking.IsHost && IsPipe && death.Victim == Shooter ) GameObject.Destroy();
	}

	internal void Detonate( Vector3? normal = null )
	{
		if ( _spent ) return;
		_spent = true;
		var origin = WorldPosition;
		if ( normal is null )
		{
			var floor = Scene.Trace.Ray( WorldPosition + Vector3.Up * 8, WorldPosition - Vector3.Up * 24 )
				.IgnoreGameObjectHierarchy( GameObject ).WithoutTags( "trigger", "debris" ).Run();
			if ( floor.Hit )
			{
				origin = floor.EndPosition;
				normal = floor.Normal;
			}
		}
		var position = origin + (normal ?? Vector3.Zero) * ((Damage - 24) * 0.6f);
		FortressBlast.Apply( GameObject, Shooter, position, Damage, falloff: 1 );
		Mount?.NpcAudio.Emit( $"weapons/explode{Game.Random.Int( 3, 5 )}.wav", position );
		ShowExplosion( position );
		GameObject.Destroy();
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void ShowExplosion( Vector3 position ) =>
		Blast.SpriteEffect( "TFC grenade explosion", "explode01", position, 1, 15, mount: Mount );
}

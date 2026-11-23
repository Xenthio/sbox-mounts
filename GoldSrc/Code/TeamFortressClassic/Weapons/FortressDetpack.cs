using GoldSrc.HalfLife;
using GoldSrc.Maps;

namespace GoldSrc.TeamFortressClassic;

[Hide]
public sealed class FortressDetpack : Component, Component.IPressable
{
	const float BlastRadius = 700;
	const float DefuseTime = 3;
	const float DefuseReach = 85;

	[Sync( SyncFlags.FromHost )] public PlayerController Planter { get; private set; }
	[Sync( SyncFlags.FromHost )] public TimeUntil FuseRemaining { get; private set; }
	[Sync( SyncFlags.FromHost )] PlayerController Disarmer { get; set; }
	TimeUntil _defuseComplete;
	GameObject _warning;
	bool _warned, _finished;

	static GameMount Mount => Sandbox.Mounting.Directory.Get( "tfc" ) as TeamFortressClassicMount;

	internal static bool Plant( PlayerController planter, Vector3 position, int fuse )
	{
		if ( Mount?.FindMountPath( "models/detpack.mdl" ) is not { } path ||
			Model.Load( $"mount://tfc/{path}.vmdl" ) is not { IsError: false } model ) return false;

		var go = new GameObject( false, "TFC detpack" ) { WorldPosition = position };
		go.AddComponent<ModelRenderer>().Model = model;
		var collider = go.AddComponent<BoxCollider>();
		collider.Scale = Vector3.One * 8;
		collider.Static = true;
		var pack = go.AddComponent<FortressDetpack>();
		pack.Planter = planter;
		pack.FuseRemaining = fuse;
		go.NetworkSpawn( true, null );
		Mount?.NpcAudio.Emit( "weapons/mine_activate.wav", position, attenuation: 0.8f );
		return true;
	}

	bool IPressable.CanPress( IPressable.Event e ) => CanDefuse( e.Source as PlayerController );
	bool IPressable.Press( IPressable.Event e )
	{
		if ( e.Source is not PlayerController player || !CanDefuse( player ) ) return false;
		SetDefusing( player, true );
		return true;
	}

	void IPressable.Release( IPressable.Event e )
	{
		if ( e.Source is PlayerController player ) SetDefusing( player, false );
	}

	IPressable.Tooltip? IPressable.GetTooltip( IPressable.Event e ) =>
		new( "Detpack", "timer", Disarmer == e.Source ? "Defusing" : "Hold Use to defuse", CanDefuse( e.Source as PlayerController ) );

	bool CanDefuse( PlayerController player ) => player.IsValid() && player.Enabled && player != Planter &&
		(!Disarmer.IsValid() || Disarmer == player) && FuseRemaining > 0 &&
		player.EyePosition.DistanceSquared( WorldPosition ) <= DefuseReach * DefuseReach &&
		MapState.PlayerVitals( player ) is { Health: > 0 };

	[Rpc.Host]
	void SetDefusing( PlayerController player, bool defusing )
	{
		if ( !player.IsValid() || (Rpc.Caller != Connection.Local && player.Network.Owner != Rpc.Caller) ) return;
		if ( !defusing )
		{
			if ( Disarmer == player ) Disarmer = null;
			return;
		}

		if ( Disarmer.IsValid() || !CanDefuse( player ) || !HasLineOfSight( player ) ) return;
		Disarmer = player;
		_defuseComplete = DefuseTime;
	}

	bool HasLineOfSight( PlayerController player ) => !Scene.Trace.Ray( player.EyePosition, WorldPosition )
		.IgnoreGameObjectHierarchy( player.GameObject ).IgnoreGameObjectHierarchy( GameObject ).WithoutTags( "trigger" ).Run().Hit;

	internal void TriggerEmp()
	{
		if ( !_finished ) FuseRemaining = Game.Random.Float( 1, 3 );
	}

	protected override void OnUpdate()
	{
		if ( _finished ) return;
		if ( Networking.IsHost )
		{
			if ( FuseRemaining <= 0 )
			{
				Detonate();
				return;
			}
			if ( Disarmer.IsValid() )
			{
				if ( !CanDefuse( Disarmer ) || !HasLineOfSight( Disarmer ) ) Disarmer = null;
				else if ( _defuseComplete <= 0 )
				{
					_finished = true;
					GameObject.Destroy();
					return;
				}
			}
			if ( !_warned && FuseRemaining <= 5 )
			{
				_warned = true;
				Mount?.NpcAudio.Emit( "weapons/mine_charge.wav", WorldPosition, attenuation: 0.8f );
			}
		}

		if ( Application.IsDedicatedServer || FuseRemaining > 5 ) return;
		if ( !_warning.IsValid() && Mount?.GetIndexedSprite( "flare3" ) is { } sprite )
		{
			_warning = new GameObject( GameObject, true, "Detpack warning flare" ) { NetworkMode = NetworkMode.Never };
			_warning.LocalPosition = Vector3.Up * 64;
			var renderer = _warning.AddComponent<SpriteRenderer>();
			renderer.Sprite = sprite;
			renderer.Additive = true;
			renderer.Size = new Vector2( 16, 16 );
		}
		if ( _warning.IsValid() ) _warning.Enabled = ((int)(float)FuseRemaining % 2) == 0;
	}

	void Detonate()
	{
		_finished = true;
		var origin = WorldPosition;
		var embedded = Scene.Trace.Ray( origin, origin + Vector3.Up ).WithTag( "world" )
			.IgnoreGameObjectHierarchy( GameObject ).Run().StartedSolid;
		if ( !embedded )
		{
			DamageInRange( origin );
			Mount?.NpcAudio.Emit( "weapons/mortarhit.wav", origin, attenuation: 0.8f );
			ShowBlast( origin );
		}
		GameObject.Destroy();
	}

	void DamageInRange( Vector3 origin )
	{
		var attacker = Planter.IsValid() ? Planter.GameObject : null;
		var targets = Scene.FindInPhysics( new Sphere( origin, BlastRadius ) )
			.Select( obj => obj.GetComponentInParent<IDamageable>() ).OfType<Component>().Distinct().ToArray();
		foreach ( var target in targets )
		{
			if ( !target.IsValid() || origin.DistanceSquared( target.WorldPosition ) > BlastRadius * BlastRadius ) continue;
			var center = Npc.TargetHullCenter( target.GameObject );
			var sightPoint = Npc.TargetBodyPosition( target.GameObject );
			var hit = Scene.Trace.Ray( origin, sightPoint ).IgnoreGameObjectHierarchy( GameObject )
				.WithoutTags( "trigger", "debris", "playercontroller" ).UseHitboxes().Run();
			if ( hit.Hit && hit.GameObject?.Root != target.GameObject.Root ) continue;
			var info = new DamageInfo { Damage = 2 * BlastRadius - origin.Distance( center ),
				Attacker = attacker, Weapon = GameObject, Origin = origin, Position = center };
			info.Tags.Add( "blast" );
			((IDamageable)target).OnDamage( info );
		}
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void ShowBlast( Vector3 origin )
	{
		if ( Mount is { } mount )
			Blast.SpriteEffect( "TFC detpack explosion", "explode01", origin, 5, 15, additive: false, mount: mount );
	}
}

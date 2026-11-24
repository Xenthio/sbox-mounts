using GoldSrc.HalfLife;
using Sandbox.Npcs;

namespace GoldSrc.ScientistSlaughterhouse;

[Hide]
public sealed class PinballLaser : LaserPulse
{
	[Sync( SyncFlags.FromHost )] BaseNpc Victim { get; set; }
	BaseNpc _shownVictim;
	Color _previousTint;
	Rigidbody _body;
	bool _bodyMoved, _carrying;
	Transform _entryTransform;
	BBox _victimHull;
	TimeUntil _nextSizeChange;

	// Release the victim if the shot leaves an open Sandbox map.
	internal override float Lifetime => 16;

	protected override void OnUpdate()
	{
		if ( Glow.IsValid() && _nextSizeChange <= 0 )
		{
			_nextSizeChange = 0.1f;
			Glow.Size = Vector2.One * (64 * Game.Random.Float( 0.25f, 0.5f ));
		}
		if ( Victim == _shownVictim ) return;
		RestoreTint();
		_shownVictim = Victim;
		if ( !Victim.IsValid() ) return;
		if ( Glow.IsValid() ) Glow.Enabled = false;
		if ( GetComponentInChildren<TrailRenderer>() is { } trail ) trail.Emitting = false;
		if ( !Victim.Renderer.IsValid() ) return;
		_previousTint = Victim.Renderer.Tint;
		Victim.Renderer.Tint = Color.Lerp( _previousTint, PulseColor, 0.25f );
	}

	internal override SceneTrace FlightTrace( Vector3 start, Vector3 end ) => _carrying
		? Scene.Trace.Box( _victimHull, start, end ).IgnoreGameObjectHierarchy( Victim.GameObject ).WithoutTags( "trigger" )
		: base.FlightTrace( start, end );

	internal override void HandleContact( SceneTraceResult hit )
	{
		if ( _carrying )
		{
			BounceVictim( hit );
			return;
		}
		var npc = hit.GameObject?.GetComponentInParent<BaseNpc>();
		if ( !npc.IsValid() || npc.Health <= 0 || npc.PhysicsCarrier.IsValid() )
		{
			base.HandleContact( hit );
			return;
		}

		Victim = npc;
		_carrying = true;
		_entryTransform = npc.WorldTransform;
		_victimHull = npc.GameObject.GetBounds() + (-_entryTransform.Position);
		npc.Movement?.Stop();
		_body = npc.GetComponent<Rigidbody>();
		_bodyMoved = _body.IsValid() && _body.MotionEnabled;
		npc.StartCarry( GameObject );
		if ( _body.IsValid() )
		{
			_body.Velocity = Vector3.Zero;
			_body.MotionEnabled = false;
		}
		WorldPosition = npc.WorldPosition + Vector3.Up;
		Velocity = Vector3.Random.Normal * 1000;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;
		if ( _carrying && (!Victim.IsValid() || Victim.Health <= 0 || Victim.PhysicsCarrier != GameObject) )
		{
			GameObject.Destroy();
			return;
		}
		base.OnFixedUpdate();
		if ( _carrying && Victim.IsValid() && Victim.Health > 0 && Victim.PhysicsCarrier == GameObject )
			Victim.WorldPosition = WorldPosition;
	}

	void BounceVictim( SceneTraceResult hit )
	{
		WorldPosition = hit.EndPosition;
		Victim.WorldPosition = WorldPosition;
		Discharge( WorldPosition );
		var other = hit.GameObject?.GetComponentInParent<IDamageable>();
		if ( other is not null || Victim.Health <= 1 )
		{
			var amount = System.MathF.Max( Victim.MaxHealth, 200 );
			Hit( other, amount, hit.EndPosition, lethalImpact: true );
			Hit( Victim, amount, hit.EndPosition, lethalImpact: true );
			GameObject.Destroy();
			return;
		}

		Hit( Victim, 1, hit.EndPosition );
		var normal = hit.Normal.IsNearlyZero() ? Vector3.Up : hit.Normal;
		WorldPosition += normal * 0.5f;
		Velocity = Vector3.Reflect( Velocity, normal );
		WorldRotation = Rotation.LookAt( Velocity );
	}

	void Hit( IDamageable target, float amount, Vector3 position, bool lethalImpact = false )
	{
		if ( target is null ) return;
		var info = new DamageInfo { Attacker = Shooter, Weapon = GameObject, Origin = WorldPosition, Position = position, Damage = amount };
		info.Tags.Add( HitTag );
		if ( lethalImpact ) info.Tags.Add( "alwaysgib" );
		target.OnDamage( info );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void Discharge( Vector3 position )
	{
		if ( !Application.IsDedicatedServer && ModelMount is { } mount )
			NpcAudio.Play( mount.NpcAudio.Find( "weapons/shock_discharge.wav" ), position, attenuation: 0.3f );
	}

	protected override void OnDestroy()
	{
		RestoreTint();
		if ( !Networking.IsHost || !Victim.IsValid() || Victim.PhysicsCarrier != GameObject ) return;
		if ( Victim.Health > 0 ) Victim.WorldTransform = _entryTransform;
		Victim.StopCarry( GameObject );
		if ( _body.IsValid() ) _body.MotionEnabled = _bodyMoved;
	}

	void RestoreTint()
	{
		if ( _shownVictim.IsValid() && _shownVictim.Renderer.IsValid() ) _shownVictim.Renderer.Tint = _previousTint;
	}
}

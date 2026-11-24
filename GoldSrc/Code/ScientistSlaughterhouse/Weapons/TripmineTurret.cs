using GoldSrc.HalfLife;
using GoldSrc.Maps;
using Sandbox.Npcs;

namespace GoldSrc.ScientistSlaughterhouse;

[Hide]
public sealed class TripmineTurret : PlacedCharge
{
	const float Range = 2048;
	const float ShotInterval = 0.2f;
	const float TrackingStep = 1;
	const float ViewDot = 0.7f;
	int _rounds = 48;
	TimeUntil _nextShot;
	GameObject _target;
	TripminePulse _lastShot;
	Angles? _aim;
	SpriteRenderer _dot;

	[Sync( SyncFlags.FromHost )] Vector3 DotPosition { get; set; }
	internal override bool HasTripBeam => false;
	internal override float BlastDamage => 50;

	internal override void CheckArmedMine()
	{
		if ( _rounds == 0 )
		{
			if ( !_lastShot.IsValid() ) Detonate();
			return;
		}

		if ( !IsTargetVisible( _target ) ) _target = FindTarget();
		var desired = _target.IsValid() ? Rotation.LookAt( Npc.TargetBodyPosition( _target ) - WorldPosition ).Angles() : WorldRotation.Angles();
		var aim = _aim ?? WorldRotation.Angles();
		aim.pitch += Step( desired.pitch - aim.pitch );
		aim.yaw += Step( desired.yaw - aim.yaw );
		_aim = aim;
		var direction = aim.ToRotation().Forward;
		var hit = Trace( WorldPosition + direction * Range );
		DotPosition = hit.EndPosition - direction * 0.5f;
		var actor = hit.GameObject?.GetComponentInParent<BaseNpc>()?.GameObject ??
			hit.GameObject?.GetComponentInParent<PlayerController>()?.GameObject;
		if ( _nextShot > 0 || !IsTarget( actor ) ) return;

		_nextShot = ShotInterval;
		_rounds--;
		_lastShot = NpcProjectile.Fire<TripminePulse>( Thrower, "mount://ssh/valve/sprites/glow01.spr",
			20, null, WorldPosition, direction, 2000, pulse => pulse.Cannon = GameObject );
		PlayShot( Game.Random.Int( 1, 2 ) );
	}

	static float Step( float difference ) => System.Math.Clamp( System.MathF.IEEERemainder( difference, 360 ), -TrackingStep, TrackingStep );

	GameObject FindTarget()
	{
		return Scene.GetAllComponents<BaseNpc>().Select( npc => npc.GameObject )
			.Concat( Scene.GetAllComponents<PlayerController>().Select( player => player.GameObject ) )
			.Where( candidate => WorldRotation.Forward.Dot( (candidate.WorldPosition - WorldPosition).Normal ) > ViewDot )
			.Where( IsTargetVisible )
			.OrderBy( candidate => candidate.WorldPosition.DistanceSquared( WorldPosition ) )
			.FirstOrDefault();
	}

	bool IsTarget( GameObject candidate ) => candidate.IsValid() && candidate != Thrower &&
		(candidate.GetComponent<BaseNpc>() is { Health: > 0 } ||
			candidate.GetComponent<PlayerController>() is { } player && MapState.PlayerVitals( player ) is { Health: > 0 });

	bool IsTargetVisible( GameObject candidate )
	{
		if ( !IsTarget( candidate ) ) return false;
		var position = Npc.TargetBodyPosition( candidate );
		if ( position.DistanceSquared( WorldPosition ) > Range * Range ) return false;
		var hit = Trace( position );
		return !hit.Hit || hit.GameObject?.Root == candidate.Root;
	}

	SceneTraceResult Trace( Vector3 end ) =>
		Scene.Trace.Ray( WorldPosition, end ).IgnoreGameObjectHierarchy( GameObject ).WithoutTags( "trigger" ).UseHitboxes().Run();

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void PlayShot( int sample )
	{
		if ( !Application.IsDedicatedServer && EffectsMount is { } mount )
			NpcAudio.Play( mount.NpcAudio.Find( $"weapons/pl_gun{sample}.wav" ), WorldPosition );
	}

	protected override void OnUpdate()
	{
		if ( !IsMineArmed || Application.IsDedicatedServer || DotPosition == Vector3.Zero ) return;
		if ( !_dot.IsValid() )
		{
			_dot = LaserSpot.Create( GameObject, EffectsMount, "Pulse cannon laser spot" );
			if ( !_dot.IsValid() ) return;
			_dot.Color = new Color32( 255, 16, 16, 128 );
		}
		_dot.WorldPosition = DotPosition;
	}
}

[Hide]
public sealed class TripminePulse : LaserPulse
{
	internal GameObject Cannon { get; set; }
	internal override GameObject CollisionOwner => Cannon;
	protected override Color Tint => new Color32( 255, 16, 16, 255 );
}

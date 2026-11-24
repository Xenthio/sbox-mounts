using GoldSrc.HalfLife;
using Sandbox.Npcs;

namespace GoldSrc.ScientistSlaughterhouse;

public sealed partial class RepulsorGun
{
	const float MinTractorDistance = 128;
	const float MaxTractorDistance = 1440;

	[Sync( SyncFlags.FromHost )] GameObject TractorTarget { get; set; }
	float _tractorDistance;
	Rigidbody _tractorBody;
	BaseNpc _tractorNpc;
	bool _previousGravity;
	LineRenderer _tractorLine;

	public override bool CanReload() => IsActive && Owner.IsValid();
	public override void Reload() => ToggleTractor();

	[Rpc.Host( NetFlags.OwnerOnly )]
	void ToggleTractor()
	{
		if ( !CanReload() || _ready > 0 ) return;
		_ready = 1;
		SetDelay( 1 );
		if ( TractorTarget.IsValid() )
		{
			ReleaseTractor();
			return;
		}

		var eye = Owner.EyeTransform;
		var alignment = 0.9f;
		GameObject best = null;
		foreach ( var obj in Scene.FindInPhysics( new Sphere( eye.Position, MaxTractorDistance ) ) )
		{
			var candidate = GetMovable( obj );
			if ( !candidate.IsValid() || candidate.Root == Owner.GameObject.Root ) continue;
			if ( candidate.GetComponent<BaseNpc>() is { } npc && npc.PhysicsCarrier.IsValid() ) continue;
			var center = GoldSrc.HalfLife.Npc.TargetHullCenter( candidate );
			var delta = center - eye.Position;
			var dot = delta.Normal.Dot( eye.Forward );
			if ( dot <= alignment || delta.Length > MaxTractorDistance ) continue;
			var trace = TraceLineOfFire( new Ray( eye.Position, delta.Normal ), delta.Length, 0 ).WithoutTags( "trigger" ).UseHitboxes().Run();
			if ( trace.Hit && trace.GameObject?.Root != candidate.Root ) continue;
			best = candidate;
			alignment = dot;
		}
		if ( !best.IsValid() ) return;

		TractorTarget = best;
		_tractorDistance = eye.Position.Distance( GoldSrc.HalfLife.Npc.TargetHullCenter( best ) ).Clamp( MinTractorDistance, MaxTractorDistance );
		_tractorNpc = best.GetComponent<BaseNpc>();
		_tractorNpc?.StartCarry( GameObject );
		if ( best.GetComponent<PlayerController>().IsValid() ) return;
		_tractorBody = best.GetComponent<Rigidbody>();
		if ( !_tractorBody.IsValid() ) return;
		_previousGravity = _tractorBody.Gravity;
		_tractorBody.Gravity = false;
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();
		if ( !Networking.IsHost || TractorTarget is null ) return;
		if ( !TractorTarget.IsValid() || !IsActive || !Owner.IsValid() ||
			(_tractorNpc is not null && (!_tractorNpc.IsValid() || _tractorNpc.Health <= 0 || _tractorNpc.PhysicsCarrier != GameObject)) ||
			(_tractorNpc is null && !GetMovable( TractorTarget ).IsValid()) )
		{
			ReleaseTractor();
			return;
		}

		var ray = Owner.EyeTransform.ForwardRay;
		var center = GoldSrc.HalfLife.Npc.TargetHullCenter( TractorTarget );
		var offset = ray.Position + ray.Forward * _tractorDistance - center;
		var body = TractorTarget.GetComponent<PlayerController>()?.Body ?? _tractorBody;
		if ( !body.IsValid() ) return;
		// GoldSrc used the held entity's push mass as the distance before movement starts.
		var deadZone = System.MathF.Max( 4, _tractorNpc.IsValid() ? 4 : body.Mass * 0.1f );
		var desired = offset.Length < deadZone ? Vector3.Zero :
			offset.Normal * System.MathF.Max( offset.Length, deadZone * 2 ) + body.Velocity * 0.2f;
		GiveVelocity( TractorTarget, desired, tractor: true );
	}

	void ReleaseTractor()
	{
		if ( _tractorBody.IsValid() )
			_tractorBody.Gravity = _previousGravity;
		_tractorNpc?.StopCarry( GameObject );
		_tractorBody = null;
		_tractorNpc = null;
		TractorTarget = null;
	}

	internal static GameObject GetMovable( GameObject obj )
	{
		if ( !obj.IsValid() ) return null;
		if ( obj.GetComponentInParent<BaseNpc>() is { Health: > 0 } npc && npc.GetComponent<Rigidbody>().IsValid() )
			return npc.GameObject;
		if ( obj.GetComponentInParent<PlayerController>() is { } player && player.Body.IsValid() )
			return player.GameObject;
		return obj.GetComponentInParent<Rigidbody>() is { MotionEnabled: true } body ? body.GameObject : null;
	}

	internal static void GiveVelocity( GameObject target, Vector3 velocity, bool tractor = false )
	{
		if ( target.GetComponent<PlayerController>() is { } player && player.Body.IsValid() )
			GoldSrc.HalfLife.Npc.PunchPlayer( player, velocity - player.Body.Velocity, Angles.Zero );
		else if ( !tractor && target.GetComponent<BaseNpc>() is { } npc )
			npc.Launch( velocity );
		else if ( target.GetComponent<Rigidbody>() is { } body )
			body.Velocity = velocity;
	}

	void UpdateTractorView()
	{
		if ( Application.IsDedicatedServer ) return;
		if ( !TractorTarget.IsValid() || !IsActive )
		{
			if ( _tractorLine.IsValid() ) _tractorLine.GameObject.Destroy();
			_tractorLine = null;
			return;
		}
		if ( !_tractorLine.IsValid() )
		{
			var go = new GameObject( GameObject, false, "Repulsor tractor beam" ) { NetworkMode = NetworkMode.Never };
			_tractorLine = go.AddComponent<LineRenderer>();
			_tractorLine.UseVectorPoints = true;
			_tractorLine.VectorPoints = [Vector3.Zero, Vector3.Zero];
			_tractorLine.Width = 5;
			_tractorLine.Color = new Color( 0, 1, 0 );
			_tractorLine.Additive = true;
			_tractorLine.Opaque = _tractorLine.CastShadows = false;
			_tractorLine.Texturing = TrailTextureConfig.Default with
			{
				Material = ContentMount?.GetTrailMaterial( "xenobeam" ),
				WorldSpace = true
			};
			go.Enabled = true;
		}
		_tractorLine.VectorPoints[0] = GetMuzzleTransform().Position;
		_tractorLine.VectorPoints[1] = GoldSrc.HalfLife.Npc.TargetHullCenter( TractorTarget );
	}
}

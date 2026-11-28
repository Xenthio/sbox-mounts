namespace UnityMount;

/// <summary>Emits from the shape and local transform stored in a mounted Unity particle system.</summary>
[Title( "Mounted Particle Shape" )]
public sealed class UnityShapeEmitter : ParticleEmitter
{
	public enum ShapeKind { Point, Box, BoxShell, Cone, Sphere, Circle, Hemisphere, Edge, Rectangle, Surface }

	internal const string ShapeRotationKey = "UnityParticleShapeRotation";

	public readonly record struct BurstEvent( float Time, ParticleFloat Count, int Cycles, float Interval, float Probability );

	[Property] public ShapeKind Shape { get; set; }
	[Property] public Transform ShapeTransform { get; set; } = global::Transform.Zero;
	[Property] public float Radius { get; set; }
	[Property] public float Thickness { get; set; } = 1;
	[Property] public float Angle { get; set; }
	[Property] public float Length { get; set; }
	[Property] public bool AlignToDirection { get; set; }
	[Property] public ParticleFloat Speed { get; set; }
	[Property] public bool SeparateStartSize { get; set; }
	[Property] public ParticleFloat StartHeight { get; set; }
	[Property] public ParticleFloat StartDepth { get; set; }
	[Property] public BurstEvent[] Bursts { get; set; } = [];
	[Property] public bool CollisionOnly { get; set; }

	internal UnityParticleSurface Surface { get; set; }

	ParticleEffect _burstTarget;
	PriorityQueue<(int Index, int Cycle), (float Time, int Index)> _scheduled;
	readonly record struct CollisionBurst( BurstEvent Burst, int Cycle, float Start, Vector3 Position, Angles Rotation );
	PriorityQueue<CollisionBurst, float> _collisionBursts;
	float _collisionTime;
	float _loopRandom;

	protected override void OnEnabled()
	{
		base.OnEnabled();
		if ( Bursts is not { Length: > 0 } ) return;
		_burstTarget = Components.GetInAncestorsOrSelf<ParticleEffect>();
		if ( !_burstTarget.IsValid() ) return;
		if ( CollisionOnly )
		{
			_collisionTime = 0;
			_collisionBursts = new();
			_burstTarget.OnPreStep += EmitCollisionBursts;
			return;
		}

		_scheduled = new();
		ScheduleBursts();
		_loopRandom = EmitRandom;
		_burstTarget.OnPreStep += EmitBursts;
	}

	protected override void OnDisabled()
	{
		if ( _burstTarget.IsValid() )
		{
			_burstTarget.OnPreStep -= EmitBursts;
			_burstTarget.OnPreStep -= EmitCollisionBursts;
		}
		_burstTarget = null;
		_scheduled = null;
		_collisionBursts = null;
		base.OnDisabled();
	}

	void ScheduleBursts()
	{
		_scheduled.Clear();
		for ( var i = 0; i < Bursts.Length; i++ )
			_scheduled.Enqueue( (i, 0), (Bursts[i].Time, i) );
	}

	void EmitBursts( float frameTime )
	{
		if ( !_burstTarget.Active ) return;
		// The native emitter resets its clock on the step that passes the end of a loop.
		var restarted = time == 0 && EmitRandom != _loopRandom;
		if ( !restarted && EmitRandom != _loopRandom ) ScheduleBursts();
		_loopRandom = EmitRandom;
		var duration = EvaluatedDuration;
		var elapsed = restarted ? duration : Math.Min( time - EvaluatedDelay, duration );

		while ( _scheduled.TryPeek( out var next, out var order ) && order.Time <= elapsed )
		{
			_scheduled.Dequeue();
			var burst = Bursts[next.Index];
			var progress = Math.Clamp( order.Time / duration, 0, 1 );
			if ( burst.Probability >= 1 || Game.Random.Float() < burst.Probability )
			{
				var count = MathF.Round( burst.Count.Evaluate( progress, Game.Random.Float() ) );
				for ( var i = 0; i < count && !_burstTarget.IsFull; i++ )
					Emit( _burstTarget, progress );
			}
			var cycle = next.Cycle + 1;
			var nextTime = burst.Time + cycle * burst.Interval;
			if ( (burst.Cycles == 0 || cycle < burst.Cycles) && nextTime > order.Time && nextTime <= duration )
				_scheduled.Enqueue( (next.Index, cycle), (nextTime, next.Index) );
		}
		if ( restarted ) ScheduleBursts();
	}

	internal void QueueCollisionBursts( Vector3 position, Angles rotation )
	{
		if ( _collisionBursts is null || !_burstTarget.Active ) return;
		foreach ( var burst in Bursts )
			_collisionBursts.Enqueue( new( burst, 0, _collisionTime, position, rotation ), _collisionTime + burst.Time );
	}

	void EmitCollisionBursts( float delta )
	{
		_collisionTime += delta;
		var duration = EvaluatedDuration;
		while ( _collisionBursts.TryPeek( out var next, out var due ) && due <= _collisionTime )
		{
			_collisionBursts.Dequeue();
			var progress = Math.Clamp( (due - next.Start) / duration, 0, 1 );
			if ( next.Burst.Probability >= 1 || Game.Random.Float() < next.Burst.Probability )
			{
				var count = MathF.Round( next.Burst.Count.Evaluate( progress, Game.Random.Float() ) );
				for ( var i = 0; i < count && !_burstTarget.IsFull; i++ )
					Emit( _burstTarget, progress, next.Position, next.Rotation );
			}
			var cycle = next.Cycle + 1;
			var nextTime = next.Start + next.Burst.Time + cycle * next.Burst.Interval;
			if ( (next.Burst.Cycles == 0 || cycle < next.Burst.Cycles) && nextTime > due && nextTime <= next.Start + duration )
				_collisionBursts.Enqueue( next with { Cycle = cycle }, nextTime );
		}
	}

	public override bool Emit( ParticleEffect target ) => !CollisionOnly && Emit( target, Delta );

	bool Emit( ParticleEffect target, float progress, Vector3? collisionPosition = null, Angles inheritedRotation = default )
	{
		var random = Game.Random;
		var offset = Vector3.Zero;
		var direction = Vector3.Forward;
		if ( Shape is ShapeKind.Sphere or ShapeKind.Hemisphere )
		{
			var sample = random.VectorInSphere();
			if ( Shape == ShapeKind.Hemisphere ) sample.x = MathF.Abs( sample.x );
			direction = sample.Normal;
			offset = direction * (Radius * float.Lerp( 1 - Thickness, 1, sample.Length ));
		}
		else if ( Shape == ShapeKind.Circle )
		{
			var (sin, cos) = MathF.SinCos( random.Float( 0, Angle ) * (MathF.PI / 180) );
			direction = new( 0, -cos, sin );
			var inner = 1 - Thickness;
			offset = direction * (Radius * MathF.Sqrt( float.Lerp( inner * inner, 1, random.Float() ) ));
		}
		else if ( Shape == ShapeKind.Edge )
		{
			offset = Vector3.Left * random.Float( -Radius, Radius );
			direction = Vector3.Up;
		}
		else if ( Shape == ShapeKind.Rectangle )
		{
			offset = UnityCoordinates.ToInches( new Vector3( 0, random.Float( -.5f, .5f ), random.Float( -.5f, .5f ) ) );
		}
		else if ( Shape is ShapeKind.Box or ShapeKind.BoxShell )
		{
			offset = new Vector3( random.Float( -.5f, .5f ), random.Float( -.5f, .5f ), random.Float( -.5f, .5f ) )
				* UnityCoordinates.ToInches( 1 );
			if ( Shape == ShapeKind.BoxShell )
			{
				var box = ShapeTransform.Scale * WorldScale;
				var yz = box.y * box.z;
				var xz = box.x * box.z;
				var face = random.Float( 0, yz + xz + box.x * box.y );
				var edge = random.Int( 0, 1 ) == 0 ? -.5f : .5f;
				var edgePosition = UnityCoordinates.ToInches( edge );
				if ( face < yz ) offset.x = edgePosition;
				else if ( face < yz + xz ) offset.y = edgePosition;
				else offset.z = edgePosition;
			}
		}
		else if ( Shape == ShapeKind.Cone )
		{
			var angle = Angle * (MathF.PI / 180);
			var (sin, cos) = MathF.SinCos( angle );
			var distance = random.Float( 0, Length );
			var azimuth = random.Float( 0, 2 * MathF.PI );
			var inner = 1 - Thickness;
			var radial = MathF.Sqrt( float.Lerp( inner * inner, 1, random.Float() ) );
			var outward = new Vector3( 0, MathF.Cos( azimuth ), MathF.Sin( azimuth ) ) * radial;
			var radius = Length > 0 ? Radius + distance * sin / cos : Radius;
			offset = new Vector3( distance, 0, 0 ) + outward * radius;
			direction = Angle == 90 && radial > 0
				? outward.Normal
				: (Vector3.Forward * cos + outward * sin).Normal;
		}

		var world = WorldTransform.ToWorld( ShapeTransform );
		if ( collisionPosition is { } point ) world.Position += point - WorldPosition;
		var position = world.PointToWorld( offset );
		if ( Shape == ShapeKind.Surface )
		{
			if ( Surface?.TrySample( out position, out var normal ) != true ) return false;
			direction = world.Rotation.Inverse * normal;
		}
		var particle = target.Emit( position, progress );
		if ( collisionPosition.HasValue )
		{
			particle.StartAngles += inheritedRotation;
			particle.Angles = particle.StartAngles;
		}
		if ( AlignToDirection )
			particle.Set( ShapeRotationKey, Rotation.FromToRotation( Vector3.Forward, ShapeTransform.Rotation * direction ) );
		if ( SeparateStartSize )
			particle.Set( UnityParticleMeshes.StartAxesKey, new Vector2(
				StartHeight.Evaluate( progress, particle.Rand( 61, 0 ) ), StartDepth.Evaluate( progress, particle.Rand( 67, 0 ) ) ) );
		particle.Velocity += world.Rotation * direction * Speed.Evaluate( progress, random.Float() );
		return true;
	}
}

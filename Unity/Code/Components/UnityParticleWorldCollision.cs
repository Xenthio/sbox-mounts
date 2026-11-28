namespace UnityMount;

/// <summary>Sweeps mounted particles against authored planes or selected world collider layers.</summary>
[Title( "Mounted Particle World Collision" )]
public sealed class UnityParticleWorldCollision : Component, Component.ExecuteInEditor
{
	const string SweepKey = "UnityParticleCollisionSweep";
	const float SurfaceClearance = .03125f;
	const int MaxContactsPerStep = 4;

	[Property] public uint Layers { get; set; }
	[Property] public bool UsePlanes { get; set; }
	[Property] public GameObject[] Planes { get; set; } = [];
	public sealed record SubEmitter( UnityShapeEmitter Emitter, float Probability, bool InheritRotation );
	[Property] public SubEmitter[] SubEmitters { get; set; } = [];
	[Property] public bool IncludeDynamic { get; set; } = true;
	[Property] public float RadiusScale { get; set; } = 1;
	[Property] public float MinimumSpeed { get; set; }
	[Property] public float MaximumSpeed { get; set; } = float.MaxValue;
	[Property] public ParticleFloat Dampen { get; set; } = 0;
	[Property] public ParticleFloat Bounce { get; set; } = 1;
	[Property] public ParticleFloat LifetimeLoss { get; set; } = 0;

	readonly List<Particle> _expired = new();
	ParticleEffect _effect;
	uint _tagMask;
	string[] _tags = [];

	protected override void OnEnabled()
	{
		_effect = GetComponent<ParticleEffect>( true );
		if ( !_effect.IsValid() ) return;
		_effect.OnStep += CaptureStart;
		_effect.OnPostStep += ResolveCollisions;
	}

	protected override void OnDisabled()
	{
		if ( _effect.IsValid() )
		{
			_effect.OnStep -= CaptureStart;
			_effect.OnPostStep -= ResolveCollisions;
		}
		_effect = null;
	}

	static void CaptureStart( Particle particle, float life )
	{
		var sweep = particle.Get<Sweep>( SweepKey );
		if ( sweep is null )
		{
			sweep = new Sweep();
			particle.Set( SweepKey, sweep );
		}
		sweep.Start = particle.Position;
		sweep.Frame = particle.Frame;
	}

	void ResolveCollisions( float delta )
	{
		if ( delta <= 0 || (UsePlanes ? Planes.Length == 0 : Layers == 0) ) return;
		if ( !UsePlanes && _tagMask != Layers )
		{
			_tagMask = Layers;
			_tags = Enumerable.Range( 0, 32 ).Where( layer => (Layers & (1u << layer)) != 0 )
				.Select( UnitySceneObjects.LayerTag ).ToArray();
		}

		var trace = default(SceneTrace);
		if ( !UsePlanes )
		{
			trace = Scene.Trace.WithAnyTags( _tags ).UseHitPosition();
			if ( !IncludeDynamic ) trace = trace.IgnoreDynamic();
		}
		_expired.Clear();
		foreach ( var particle in _effect.Particles )
		{
			var sweep = particle.Get<Sweep>( SweepKey );
			if ( sweep?.Frame != particle.Frame ) continue;
			if ( SweepParticle( particle, sweep.Start, trace ) ) _expired.Add( particle );
		}

		foreach ( var particle in _expired ) _effect.Terminate( particle );
		_expired.Clear();
	}

	bool SweepParticle( Particle particle, Vector3 start, SceneTrace trace )
	{
		var movement = particle.Position - start;
		var radius = MathF.Max( 0, MathF.Abs( particle.Size.x ) * .5f * RadiusScale );
		for ( var i = 0; i < MaxContactsPerStep && movement.LengthSquared > .000001f; i++ )
		{
			var contact = UsePlanes ? TracePlanes( start, movement, radius ) : TraceWorld( trace, start, movement, radius );
			if ( contact is not { } hit )
			{
				particle.Position = start + movement;
				return false;
			}

			var bounce = Math.Clamp( Bounce.Evaluate( particle, 127, 0 ), 0, 1 );
			var retainedSpeed = 1 - Math.Clamp( Dampen.Evaluate( particle, 131, 0 ), 0, 1 );
			particle.Velocity = Deflect( particle.Velocity, hit.Normal, bounce ) * retainedSpeed;
			particle.HitNormal = hit.Normal;
			particle.HitPos = hit.HitPosition;
			particle.LastHitTime = particle.HitTime = Time.Now;
			foreach ( var sub in SubEmitters )
				if ( sub.Emitter.IsValid() && (sub.Probability >= 1f || Game.Random.Float() < sub.Probability) )
					sub.Emitter.QueueCollisionBursts( hit.HitPosition, sub.InheritRotation ? particle.Angles : default );
			start = hit.EndPosition + hit.Normal * SurfaceClearance;
			particle.Position = start;

			// Age advances on impact because s&box re-evaluates DeathTime from the authored lifetime each step.
			var lifetime = particle.DeathTime - particle.BornTime;
			var loss = Math.Clamp( LifetimeLoss.Evaluate( particle, 137, 0 ), 0, 1 );
			particle.Age += lifetime * loss;
			var speed = particle.Velocity.Length;
			if ( particle.Age >= lifetime || speed < MinimumSpeed || speed > MaximumSpeed ) return true;
			particle.LifeDelta = particle.Age / lifetime;
			movement = Deflect( movement * (1 - hit.Fraction), hit.Normal, bounce ) * retainedSpeed;
		}
		return false;
	}

	Contact? TracePlanes( Vector3 start, Vector3 movement, float radius )
	{
		Contact? nearest = null;
		foreach ( var plane in Planes )
		{
			if ( !plane.IsValid() ) continue;
			var normal = plane.WorldRotation.Up;
			var distance = (start - plane.WorldPosition).Dot( normal ) - radius;
			var travel = movement.Dot( normal );
			if ( distance < 0 || travel >= 0 || distance + travel > 0 ) continue;
			var fraction = distance / -travel;
			if ( nearest is { } candidate && fraction >= candidate.Fraction ) continue;
			var center = start + movement * fraction;
			nearest = new( fraction, center, center - normal * radius, normal );
		}
		return nearest;
	}

	static Contact? TraceWorld( SceneTrace trace, Vector3 start, Vector3 movement, float radius )
	{
		var hit = trace.Sphere( radius, start, start + movement ).Run();
		return hit.Hit && !hit.StartedSolid ? new( hit.Fraction, hit.EndPosition, hit.HitPosition, hit.Normal ) : null;
	}

	readonly record struct Contact( float Fraction, Vector3 EndPosition, Vector3 HitPosition, Vector3 Normal );

	static Vector3 Deflect( Vector3 velocity, Vector3 normal, float bounce ) =>
		velocity - normal * (MathF.Min( 0, Vector3.Dot( velocity, normal ) ) * (1 + bounce));

	sealed class Sweep
	{
		public Vector3 Start;
		public int Frame;
	}
}

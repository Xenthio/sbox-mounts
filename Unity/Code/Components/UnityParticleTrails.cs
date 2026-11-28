using System.Buffers;

namespace UnityMount;

/// <summary>Draws trails behind individual particles or ribbons connecting live particles.</summary>
[Title( "Mounted Particle Trails" )]
public sealed class UnityParticleTrails : Component, Component.ExecuteInEditor, Component.DontExecuteOnServer
{
	[Property] public Material Material { get; set; }
	[Property] public bool Opaque { get; set; }
	[Property] public int RibbonCount { get; set; }
	[Property] public bool AttachRibbonToTransform { get; set; }
	[Property] public bool RepeatPerSegment { get; set; }
	[Property] public float Ratio { get; set; } = 1;
	[Property] public float PointDistance { get; set; }
	[Property] public bool LocalSpace { get; set; }
	[Property] public bool SizeAffectsWidth { get; set; } = true;
	[Property] public bool InheritColor { get; set; } = true;
	[Property] public ParticleFloat Lifetime { get; set; } = 1;
	[Property] public ParticleFloat Width { get; set; } = 1;
	[Property] public ParticleGradient ColorOverLifetime { get; set; } = Color.White;
	[Property] public ParticleGradient ColorOverTrail { get; set; } = Color.White;

	readonly Dictionary<Particle, Trail> _trails = new();
	readonly List<TrailPoint> _points = new();
	readonly List<int> _ends = new();
	readonly List<(Vector3 Position, Particle Particle, float Distance)> _ribbon = new();
	ParticleEffect _effect;
	TrailSceneObject _renderer;

	protected override void OnEnabled()
	{
		_effect = GetComponent<ParticleEffect>( true );
		if ( !_effect.IsValid() || Material is null || Material.IsError ) return;
		_renderer = new TrailSceneObject( Scene.SceneWorld, Material, Opaque, RepeatPerSegment );
		_effect.OnPostStep += UpdateTrails;
		if ( RibbonCount == 0 )
		{
			_effect.OnParticleCreated += StartTrail;
			_effect.OnParticleDestroyed += RemoveTrail;
			foreach ( var particle in _effect.Particles ) StartTrail( particle );
		}
	}

	protected override void OnDisabled()
	{
		if ( _effect.IsValid() )
		{
			_effect.OnParticleCreated -= StartTrail;
			_effect.OnParticleDestroyed -= RemoveTrail;
			_effect.OnPostStep -= UpdateTrails;
		}
		_renderer?.Delete();
		_renderer = null;
		_effect = null;
		_trails.Clear();
		_ribbon.Clear();
	}

	void StartTrail( Particle particle )
	{
		if ( Ratio < 1 && particle.Rand( 41, 0 ) >= Ratio ) return;
		var position = LocalSpace ? WorldTransform.PointToLocal( particle.Position ) : particle.Position;
		var trail = new Trail();
		trail.Add( position, SampleExpiry( particle ) );
		_trails[particle] = trail;
	}

	void RemoveTrail( Particle particle ) => _trails.Remove( particle );

	float SampleExpiry( Particle particle )
	{
		var multiplier = Lifetime.Evaluate( particle.LifeDelta, particle.Rand( 43, 0 ) );
		return particle.Age + particle.LifeTimeRemaining * multiplier;
	}

	void UpdateTrails( float delta )
	{
		_points.Clear();
		_ends.Clear();
		var transform = WorldTransform;
		var bounds = BBox.FromPositionAndSize( transform.Position, 0 );
		if ( RibbonCount > 0 )
		{
			bounds = AddRibbons( bounds );
			_renderer.Bounds = bounds;
			_renderer.Update( new( _points.ToArray(), _ends.ToArray() ) );
			return;
		}
		foreach ( var (particle, trail) in _trails )
		{
			trail.Samples.RemoveAll( sample => particle.Age > sample.Expiry );

			var head = LocalSpace ? transform.PointToLocal( particle.Position ) : particle.Position;
			var distance = trail.Last.DistanceSquared( head );
			if ( trail.Samples.Count == 0 || (distance > 0 && distance >= PointDistance * PointDistance) )
				trail.Add( head, SampleExpiry( particle ) );

			var start = _points.Count;
			var previous = Vector3.Zero;
			var length = 0f;
			foreach ( var sample in trail.Samples ) AddPoint( sample.Position );
			if ( trail.Last != head ) AddPoint( head );
			if ( length <= 0 )
			{
				_points.RemoveRange( start, _points.Count - start );
				continue;
			}

			var tint = ColorOverLifetime.Evaluate( particle.LifeDelta, particle.Rand( 47, 0 ) );
			if ( InheritColor ) tint *= particle.Color.WithAlpha( particle.Color.a * particle.Alpha );
			var baseWidth = SizeAffectsWidth ? particle.Size.x : UnityCoordinates.ToInches( 1 );
			var widthRandom = particle.Rand( 53, 0 );
			var colorRandom = particle.Rand( 59, 0 );
			for ( var i = start; i < _points.Count; i++ )
			{
				var point = _points[i];
				var progress = 1 - point.U / length;
				var halfWidth = MathF.Max( 0, baseWidth * Width.Evaluate( progress, widthRandom ) * 0.5f );
				var color = tint * ColorOverTrail.Evaluate( progress, colorRandom );
				_points[i] = new( point.Position, progress, halfWidth, color );
				bounds = bounds.AddBBox( new BBox( point.Position - halfWidth, point.Position + halfWidth ) );
			}
			_ends.Add( _points.Count );

			void AddPoint( Vector3 position )
			{
				if ( LocalSpace ) position = transform.PointToWorld( position );
				if ( _points.Count > start )
				{
					var distance = previous.Distance( position );
					if ( distance <= 0.0001f ) return;
					length += distance;
				}
				_points.Add( new( position, length, 0, default ) );
				previous = position;
			}
		}
		_renderer.Bounds = bounds;
		_renderer.Update( new( _points.ToArray(), _ends.ToArray() ) );
	}

	BBox AddRibbons( BBox bounds )
	{
		var particles = _effect.Particles.Where( p => Ratio >= 1 || p.Rand( 41, 0 ) < Ratio )
			.OrderByDescending( p => p.Age ).ToArray();
		for ( var ribbon = 0; ribbon < Math.Min( RibbonCount, particles.Length ); ++ribbon )
		{
			_ribbon.Clear();
			var length = 0f;
			// The existing trail renderer takes points from the tail to the head.
			for ( var i = ribbon; i < particles.Length; i += RibbonCount ) Add( particles[i].Position, particles[i] );
			if ( AttachRibbonToTransform ) Add( WorldPosition, _ribbon[^1].Particle );
			if ( length <= 0 ) continue;

			foreach ( var (position, particle, distance) in _ribbon )
			{
				var progress = 1 - distance / length;
				var tint = ColorOverLifetime.Evaluate( particle.LifeDelta, particle.Rand( 47, 0 ) );
				if ( InheritColor ) tint *= particle.Color.WithAlpha( particle.Color.a * particle.Alpha );
				var baseWidth = SizeAffectsWidth ? particle.Size.x : UnityCoordinates.ToInches( 1 );
				var halfWidth = MathF.Max( 0, baseWidth * Width.Evaluate( progress, particle.Rand( 53, 0 ) ) * .5f );
				var color = tint * ColorOverTrail.Evaluate( progress, particle.Rand( 59, 0 ) );
				_points.Add( new( position, progress, halfWidth, color ) );
				bounds = bounds.AddBBox( new BBox( position - halfWidth, position + halfWidth ) );
			}
			_ends.Add( _points.Count );

			void Add( Vector3 position, Particle particle )
			{
				if ( _ribbon.Count > 0 )
				{
					var spacing = position.Distance( _ribbon[^1].Position );
					if ( spacing <= 0.0001f ) return;
					length += spacing;
				}
				_ribbon.Add( (position, particle, length) );
			}
		}
		_ribbon.Clear();
		return bounds;
	}

	sealed class Trail
	{
		public readonly List<(Vector3 Position, float Expiry)> Samples = new();
		public Vector3 Last { get; private set; }

		public void Add( Vector3 position, float expiry )
		{
			Samples.Add( (position, expiry) );
			Last = position;
		}
	}

	readonly record struct TrailPoint( Vector3 Position, float U, float HalfWidth, Color32 Color );
	sealed record TrailSnapshot( TrailPoint[] Points, int[] Ends );

	sealed class TrailSceneObject : SceneCustomObject
	{
		readonly Material _material;
		readonly bool _repeatPerSegment;
		volatile TrailSnapshot _snapshot = new( [], [] );

		public TrailSceneObject( SceneWorld world, Material material, bool opaque, bool repeatPerSegment ) : base( world )
		{
			_material = material;
			_repeatPerSegment = repeatPerSegment;
			Flags.IsOpaque = opaque;
			Flags.IsTranslucent = !opaque;
			Flags.CastShadows = false;
		}

		public void Update( TrailSnapshot snapshot ) => _snapshot = snapshot;

		public override void RenderSceneObject()
		{
			var (points, ends) = _snapshot;
			if ( ends.Length == 0 ) return;
			if ( Flags.IsOpaque ) Graphics.SetupLighting( this );
			var vertices = ArrayPool<Vertex>.Shared.Rent( (points.Length - ends.Length) * 6 );
			try
			{
				var camera = Graphics.CameraRotation;
				var index = 0;
				var start = 0;
				foreach ( var end in ends )
				{
					var (left, right) = Edges( start, end, start, _repeatPerSegment ? 1 : null );
					for ( var i = start + 1; i < end; i++ )
					{
						var (nextLeft, nextRight) = Edges( start, end, i, _repeatPerSegment ? 0 : null );
						vertices[index++] = left;
						vertices[index++] = nextRight;
						vertices[index++] = right;
						vertices[index++] = left;
						vertices[index++] = nextLeft;
						vertices[index++] = nextRight;
						(left, right) = _repeatPerSegment ? Edges( start, end, i, 1 ) : (nextLeft, nextRight);
					}
					start = end;
				}
				Graphics.Draw( vertices.AsSpan(), index, _material );

				(Vertex Left, Vertex Right) Edges( int start, int end, int index, float? u )
				{
					var point = points[index];
					var tangent = (points[Math.Min( index + 1, end - 1 )].Position - points[Math.Max( start, index - 1 )].Position).Normal;
					var across = Vector3.Cross( tangent, camera.Forward ).Normal * point.HalfWidth;
					return (Corner( -across, 0 ), Corner( across, 1 ));

					Vertex Corner( Vector3 offset, float v ) =>
						new( point.Position + offset, -camera.Forward, tangent, new Vector4( u ?? point.U, v, 0, 0 ) ) { Color = point.Color };
				}
			}
			finally
			{
				ArrayPool<Vertex>.Shared.Return( vertices );
			}
		}
	}
}

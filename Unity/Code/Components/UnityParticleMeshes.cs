namespace UnityMount;

/// <summary>Draws mesh particles with the model, alignment and material authored in Unity.</summary>
[Title( "Mounted Particle Meshes" )]
public sealed class UnityParticleMeshes : Component, Component.ExecuteInEditor, Component.DontExecuteOnServer
{
	public enum RenderAlignment { View, World, Local, Facing, Velocity }
	public readonly record struct ModelChoice( Model Model, float Weight );

	internal const string StartAxesKey = "UnityParticleMeshStartAxes";

	[Property] public Model Model { get; set; }
	[Property] public ModelChoice[] Choices { get; set; } = [];
	[Property] public bool Opaque { get; set; }
	[Property] public RenderAlignment Alignment { get; set; }
	[Property] public bool UseUnityEulerAngles { get; set; }
	[Property] public Vector3 Pivot { get; set; }
	[Property] public Vector3 Flip { get; set; }
	[Property] public bool SeparateLifetimeSize { get; set; }
	[Property] public ParticleFloat HeightOverLife { get; set; } = 1;
	[Property] public ParticleFloat DepthOverLife { get; set; } = 1;

	ParticleEffect _effect;
	MeshSceneObject _renderer;
	WeightedModel[] _models = [];

	protected override void OnEnabled()
	{
		_effect = GetComponent<ParticleEffect>( true );
		if ( !_effect.IsValid() ) return;
		var choices = Choices is { Length: > 0 } ? Choices : [new( Model, 1 )];
		var models = new List<WeightedModel>( choices.Length );
		double total = 0;
		foreach ( var choice in choices )
		{
			if ( !float.IsFinite( choice.Weight ) || choice.Weight <= 0 ) continue;
			if ( choice.Model is null || choice.Model.IsError ) return;
			var bounds = choice.Model.Bounds;
			total += choice.Weight;
			models.Add( new( choice.Model, bounds.Center.Length + bounds.Extents.Length, total ) );
		}
		if ( models.Count == 0 ) return;
		_models = models.ToArray();
		_renderer = new MeshSceneObject( Scene.SceneWorld, Opaque );
		_effect.OnPostStep += UpdateMeshes;
	}

	protected override void OnDisabled()
	{
		if ( _effect.IsValid() ) _effect.OnPostStep -= UpdateMeshes;
		_renderer?.Delete();
		_renderer = null;
		_effect = null;
		_models = [];
	}

	WeightedModel PickModel( Particle particle )
	{
		if ( _models.Length == 1 ) return _models[0];
		var sample = particle.Rand( 97, 0 ) * _models[^1].CumulativeWeight;
		foreach ( var model in _models )
			if ( sample < model.CumulativeWeight ) return model;
		return _models[^1];
	}

	void UpdateMeshes( float delta )
	{
		var particles = new MeshParticle[_effect.Particles.Count];
		var bounds = BBox.FromPositionAndSize( WorldPosition, 0 );
		for ( var i = 0; i < particles.Length; i++ )
		{
			var particle = _effect.Particles[i];
			var model = PickModel( particle );
			var width = particle.Size.x;
			var height = width;
			var depth = width;
			var initial = particle.Get<Vector2?>( StartAxesKey );
			if ( initial.HasValue || SeparateLifetimeSize )
			{
				var axes = initial ?? new Vector2( particle.StartScale, particle.StartScale );
				if ( !SeparateLifetimeSize && particle.StartScale != 0 )
				{
					var growth = width / particle.StartScale;
					height = axes.x * growth;
					depth = axes.y * growth;
				}
				else
				{
					var noiseScale = UnityParticleNoise.SizeMultiplier( particle );
					height = noiseScale * axes.x * HeightOverLife.Evaluate( particle.LifeDelta, particle.Rand( 71, 0 ) );
					depth = noiseScale * axes.y * DepthOverLife.Evaluate( particle.LifeDelta, particle.Rand( 73, 0 ) );
				}
			}

			var size = new Vector3( depth, width, height );
			var scale = size / UnityCoordinates.ToInches( 1 );
			if ( particle.Rand( 79, 0 ) < Flip.x ) scale.x = -scale.x;
			if ( particle.Rand( 83, 0 ) < Flip.y ) scale.y = -scale.y;
			if ( particle.Rand( 89, 0 ) < Flip.z ) scale.z = -scale.z;
			var pivotOffset = Pivot * size;
			var radius = model.Radius * MathF.Max( MathF.Abs( scale.x ), MathF.Max( MathF.Abs( scale.y ), MathF.Abs( scale.z ) ) )
				+ pivotOffset.Length;
			bounds = bounds.AddBBox( new BBox( particle.Position - radius, particle.Position + radius ) );
			var shapeRotation = particle.Get<Rotation?>( UnityShapeEmitter.ShapeRotationKey ) ?? Rotation.Identity;
			var angles = particle.Angles;
			// The native particle effect accumulates each channel independently. Convert Unity's Euler order at draw time.
			var rotation = UseUnityEulerAngles ? UnityCoordinates.EulerRotation( new( angles.pitch, angles.yaw, angles.roll ) ) : angles.ToRotation();
			particles[i] = new( model.Model, particle.Position, particle.Velocity, scale, pivotOffset, shapeRotation, rotation,
				particle.Color.WithAlphaMultiplied( particle.Alpha ) );
		}
		_renderer.Bounds = bounds;
		_renderer.Update( new( particles, WorldRotation, Alignment ) );
	}

	readonly record struct WeightedModel( Model Model, float Radius, double CumulativeWeight );
	readonly record struct MeshParticle( Model Model, Vector3 Position, Vector3 Velocity, Vector3 Scale, Vector3 Pivot,
		Rotation ShapeRotation, Rotation ParticleRotation, Color Color );
	sealed record MeshSnapshot( MeshParticle[] Particles, Rotation LocalRotation, RenderAlignment Alignment );

	sealed class MeshSceneObject : SceneCustomObject
	{
		readonly RenderAttributes _attributes = new();
		volatile MeshSnapshot _snapshot = new( [], Rotation.Identity, RenderAlignment.World );

		public MeshSceneObject( SceneWorld world, bool opaque ) : base( world )
		{
			Flags.IsOpaque = opaque;
			Flags.IsTranslucent = !opaque;
			Flags.CastShadows = false;
		}

		public void Update( MeshSnapshot snapshot ) => _snapshot = snapshot;

		public override void RenderSceneObject()
		{
			var (particles, localRotation, alignment) = _snapshot;
			if ( particles.Length == 0 ) return;
			if ( Flags.IsOpaque ) Graphics.SetupLighting( this, _attributes );

			var camera = Graphics.CameraRotation;
			var view = Rotation.LookAt( -camera.Forward, camera.Up );
			Span<Transform> transform = stackalloc Transform[1];
			foreach ( var particle in particles )
			{
				var rotation = alignment switch
				{
					RenderAlignment.Local => localRotation,
					RenderAlignment.View => view,
					RenderAlignment.Facing => FacingRotation( Graphics.CameraPosition - particle.Position, camera.Up, view ),
					RenderAlignment.Velocity => FacingRotation( particle.Velocity, Vector3.Up, localRotation ),
					_ => Rotation.Identity
				};
				rotation *= particle.ShapeRotation * particle.ParticleRotation;
				transform[0] = new( particle.Position - rotation * particle.Pivot, rotation, particle.Scale );
				var color = particle.Color;
				_attributes.Set( "UnityParticleTint", new Vector4( color.r, color.g, color.b, color.a ) );
				Graphics.DrawModelInstanced( particle.Model, transform, _attributes );
			}
		}

		static Rotation FacingRotation( Vector3 forward, Vector3 up, Rotation fallback )
		{
			if ( forward.IsNearlyZero() ) return fallback;
			if ( Vector3.Cross( forward.Normal, up ).IsNearlyZero() )
				up = MathF.Abs( Vector3.Dot( forward.Normal, Vector3.Up ) ) < .99f ? Vector3.Up : Vector3.Right;
			return Rotation.LookAt( forward, up );
		}
	}
}

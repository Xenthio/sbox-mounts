namespace UnityMount;

readonly record struct UnityParticleAppearance( string Path, bool Additive = false, bool Material = false, bool Opaque = false, bool Refractive = false );
readonly record struct UnityParticleStretch( float LengthScale, float VelocityScale, Vector2 Pivot, bool Freeform, bool RotateWithDirection );
readonly record struct UnityParticleCollisionBinding( UnityAssetPointer Node, float Probability, bool InheritRotation );
readonly record struct UnityParticlePrimaryRenderer( UnityParticleAppearance? Billboard, UnityParticleMeshAppearance Mesh )
{
	public bool HasRenderer => Billboard is not null || Mesh is not null;
}
readonly record struct UnityParticleRendererVariant( UnityAssetPointer Material, UnityParticlePrimaryRenderer Renderer );
sealed record UnityParticleMeshAppearance( UnityParticleMeshAppearance.Choice[] Choices, UnityParticleMeshes.RenderAlignment Alignment,
	Vector3 Pivot, Vector3 Flip, bool Opaque )
{
	public readonly record struct Choice( string Path, float Weight );
}

sealed class UnityParticleSpriteLoader( string texture ) : ResourceLoader<GameMount>
{
	protected override object Load() => Sprite.FromTexture( Texture.Load( texture, false ) );
}

sealed record UnityPrefabParticle( UnityParticleEmitter Source, UnityParticlePrimaryRenderer Primary, UnityParticleAppearance? TrailAppearance,
	float Gravity, UnityParticleStretch? Stretch, float MaxViewportFraction, UnityAssetPointer[] Planes, UnityParticleCollisionBinding[] CollisionEmitters,
	UnityParticleRendererVariant[] Variants )
{
	public void AddTo( GameObject gameObject, UnityPrefabBindings bindings )
	{
		var effect = gameObject.AddComponent<ParticleEffect>( false );
		effect.Lifetime = Source.Lifetime;
		effect.TimeScale = Source.TimeScale;
		effect.PreWarm = Source.Loop && Source.Prewarm ? Source.Duration : 0;
		effect.MaxParticles = Source.Limit;
		effect.LocalSpace = Source.Local ? 1f : 0f;
		effect.ApplyColor = !Source.MinimumTint.HasValue;
		effect.Tint = Source.MinimumTint.HasValue ? Color.White : Source.Tint;
		effect.Gradient = Source.ColorOverLife;
		if ( Source.MinimumTint is { } minimum )
		{
			var startColor = gameObject.AddComponent<UnityParticleStartColor>( false );
			startColor.Minimum = minimum;
			startColor.Maximum = Source.Tint;
			startColor.Enabled = true;
		}
		effect.ApplyShape = true;
		effect.InitialScale = Source.Size;
		effect.Scale = Source.SizeOverLife;
		effect.Force = true;
		effect.ForceDirection = Vector3.Down * UnityCoordinates.ToInches( Gravity );
		effect.ForceScale = Source.Gravity;
		effect.ApplyRotation = true;
		effect.InitialPitch = Source.StartRotation.X;
		effect.InitialYaw = Source.StartRotation.Y;
		effect.InitialRoll = Source.StartRotation.Z;
		effect.AngularVelocity = Source.Spin;
		if ( Source.Velocity is { } velocity )
		{
			var motion = gameObject.AddComponent<UnityParticleMotion>( false );
			motion.Linear = velocity.Linear;
			motion.Orbital = velocity.Orbital;
			motion.WorldSpace = velocity.WorldSpace;
			motion.Enabled = true;
		}
		if ( Source.Collision is { } collision )
		{
			var worldCollision = gameObject.AddComponent<UnityParticleWorldCollision>( false );
			worldCollision.Layers = collision.Layers;
			worldCollision.IncludeDynamic = collision.IncludeDynamic;
			worldCollision.RadiusScale = collision.RadiusScale;
			worldCollision.MinimumSpeed = collision.MinimumSpeed;
			worldCollision.MaximumSpeed = collision.MaximumSpeed;
			worldCollision.Dampen = collision.Dampen;
			worldCollision.Bounce = collision.Bounce;
			worldCollision.LifetimeLoss = collision.LifetimeLoss;
			worldCollision.UsePlanes = Planes is not null;
			if ( Planes is { Length: > 0 } ) bindings.AddParticlePlanes( worldCollision, Planes );
			if ( CollisionEmitters is { Length: > 0 } ) bindings.AddCollisionEmitters( worldCollision, CollisionEmitters );
			worldCollision.Enabled = true;
		}
		if ( Source.SpeedRotation is { } rotation )
		{
			var spin = gameObject.AddComponent<UnityParticleRotationBySpeed>( false );
			spin.AngularVelocity = rotation.AngularVelocity;
			spin.SpeedRange = rotation.SpeedRange;
			spin.Enabled = true;
		}

		if ( Source.Noise is { } noiseSettings )
		{
			var noise = gameObject.AddComponent<UnityParticleNoise>( false );
			noise.Strength = noiseSettings.Strength;
			noise.Frequency = noiseSettings.Frequency;
			noise.Damping = noiseSettings.Damping;
			noise.Quality = noiseSettings.Quality;
			noise.Octaves = noiseSettings.Octaves;
			noise.OctaveMultiplier = noiseSettings.OctaveMultiplier;
			noise.OctaveScale = noiseSettings.OctaveScale;
			noise.ScrollSpeed = noiseSettings.ScrollSpeed;
			noise.RemapEnabled = noiseSettings.Remap.HasValue;
			noise.Remap = noiseSettings.Remap.GetValueOrDefault();
			noise.PositionAmount = noiseSettings.PositionAmount;
			noise.RotationAmount = noiseSettings.RotationAmount;
			noise.SizeAmount = noiseSettings.SizeAmount;
			noise.LocalSpace = Source.Local;
			noise.Duration = Source.Duration;
			noise.Loop = Source.Loop;
			noise.Enabled = true;
		}

		var emitter = AddEmitter( gameObject );
		emitter.Duration = Source.Duration;
		emitter.Delay = Source.Delay;
		emitter.Loop = Source.Loop;
		emitter.Rate = Source.Rate;
		emitter.RateOverDistance = Source.DistanceRate;
		emitter.Burst = 0;
		var primaryRenderer = AddPrimaryRenderer( gameObject, Primary );
		if ( primaryRenderer is not null ) primaryRenderer.Enabled = true;
		foreach ( var variant in Variants )
		{
			var renderer = variant.Renderer == Primary ? primaryRenderer : AddPrimaryRenderer( gameObject, variant.Renderer );
			if ( renderer is not null ) bindings.AddParticleRendererVariant( gameObject, variant.Material, renderer );
		}
		if ( TrailAppearance is { } trail )
		{
			var trails = gameObject.AddComponent<UnityParticleTrails>( false );
			trails.Material = Material.Load( trail.Path );
			trails.Opaque = trail.Opaque;
			trails.Ratio = Source.Trail.Ratio;
			trails.PointDistance = Source.Trail.PointDistance;
			trails.LocalSpace = Source.Local && !Source.Trail.WorldSpace;
			trails.SizeAffectsWidth = Source.Trail.SizeAffectsWidth;
			trails.InheritColor = Source.Trail.InheritColor;
			trails.Lifetime = Source.Trail.Lifetime;
			trails.Width = Source.Trail.Width;
			trails.ColorOverLifetime = Source.Trail.ColorOverLifetime;
			trails.ColorOverTrail = Source.Trail.ColorOverTrail;
			trails.RibbonCount = Source.Trail.RibbonCount;
			trails.AttachRibbonToTransform = Source.Trail.AttachRibbonToTransform;
			trails.RepeatPerSegment = Source.Trail.RepeatPerSegment;
			trails.Enabled = true;
		}
		effect.Enabled = true;
		emitter.Enabled = Source.PlayOnAwake;
	}

	Component AddPrimaryRenderer( GameObject gameObject, UnityParticlePrimaryRenderer visual )
	{
		if ( visual.Billboard is { Material: true } billboard )
		{
			var billboards = gameObject.AddComponent<UnityParticleBillboards>( false );
			billboards.Material = Material.Load( billboard.Path );
			billboards.Opaque = billboard.Opaque;
			billboards.Refractive = billboard.Refractive;
			billboards.UseSeparateHeight = Source.Height.HasValue;
			billboards.Height = Source.Height.GetValueOrDefault();
			billboards.MaxViewportFraction = MaxViewportFraction;
			if ( Stretch is { } stretch )
			{
				billboards.StretchToVelocity = true;
				billboards.LengthScale = stretch.LengthScale;
				billboards.VelocityScale = stretch.VelocityScale;
				billboards.Pivot = stretch.Pivot;
				billboards.FreeformStretch = stretch.Freeform;
				billboards.RotateWithStretchDirection = stretch.RotateWithDirection;
			}
			return billboards;
		}
		if ( visual.Billboard is { } appearance )
		{
			var sprite = gameObject.AddComponent<ParticleSpriteRenderer>( false );
			sprite.Sprite = GameResource.Load<Sprite>( appearance.Path );
			sprite.Additive = appearance.Additive;
			return sprite;
		}
		if ( visual.Mesh is { } mesh )
		{
			var models = gameObject.AddComponent<UnityParticleMeshes>( false );
			if ( mesh.Choices.Length == 1 ) models.Model = Model.Load( mesh.Choices[0].Path );
			else models.Choices = mesh.Choices.Select( choice => new UnityParticleMeshes.ModelChoice(
				Model.Load( choice.Path ), choice.Weight ) ).ToArray();
			models.Alignment = mesh.Alignment;
			models.UseUnityEulerAngles = Source.Rotation3D;
			models.Opaque = mesh.Opaque;
			models.Pivot = mesh.Pivot;
			models.Flip = mesh.Flip;
			models.SeparateLifetimeSize = Source.AxisSizeOverLife.HasValue;
			models.HeightOverLife = Source.AxisSizeOverLife?.Height ?? Source.SizeOverLife;
			models.DepthOverLife = Source.AxisSizeOverLife?.Depth ?? Source.SizeOverLife;
			return models;
		}
		return null;
	}

	ParticleEmitter AddEmitter( GameObject gameObject )
	{
		var shape = Source.Shape;
		var hasMesh = Primary.Mesh is not null || Variants.Any( variant => variant.Renderer.Mesh is not null );
		if ( shape.Type == UnityParticleShape.Kind.Sphere && Source.Bursts.Length == 0 && (!hasMesh || !Source.Height.HasValue) )
		{
			if ( shape.Transform != Transform.Zero )
				gameObject = new GameObject( gameObject, true, "Emission shape" ) { LocalTransform = shape.Transform };
			var sphere = gameObject.AddComponent<ParticleSphereEmitter>( false );
			sphere.Radius = shape.Radius;
			sphere.MinimumRadius = shape.Radius * (1 - shape.Thickness);
			sphere.Velocity = 0;
			sphere.RadialVelocity = Source.Speed;
			return sphere;
		}
		var emitter = gameObject.AddComponent<UnityShapeEmitter>( false );
		emitter.Shape = shape.Type switch
		{
			UnityParticleShape.Kind.Sphere => UnityShapeEmitter.ShapeKind.Sphere,
			UnityParticleShape.Kind.Hemisphere => UnityShapeEmitter.ShapeKind.Hemisphere,
			UnityParticleShape.Kind.Box => UnityShapeEmitter.ShapeKind.Box,
			UnityParticleShape.Kind.BoxShell => UnityShapeEmitter.ShapeKind.BoxShell,
			UnityParticleShape.Kind.Cone or UnityParticleShape.Kind.ConeVolume => UnityShapeEmitter.ShapeKind.Cone,
			UnityParticleShape.Kind.Circle => UnityShapeEmitter.ShapeKind.Circle,
			UnityParticleShape.Kind.Edge => UnityShapeEmitter.ShapeKind.Edge,
			UnityParticleShape.Kind.Rectangle => UnityShapeEmitter.ShapeKind.Rectangle,
			UnityParticleShape.Kind.SkinnedMeshRenderer => UnityShapeEmitter.ShapeKind.Surface,
			_ => UnityShapeEmitter.ShapeKind.Point
		};
		emitter.ShapeTransform = shape.Transform;
		emitter.Radius = shape.Radius;
		emitter.Thickness = shape.Thickness;
		emitter.Angle = shape.Angle;
		emitter.Length = shape.Length;
		emitter.AlignToDirection = shape.AlignToDirection;
		emitter.Speed = Source.Speed;
		if ( hasMesh && Source.Height is { } height && Source.Depth is { } depth )
		{
			emitter.SeparateStartSize = true;
			emitter.StartHeight = height;
			emitter.StartDepth = depth;
		}
		emitter.Bursts = Source.Bursts.Select( burst => new UnityShapeEmitter.BurstEvent(
			burst.Time, burst.Count, burst.Cycles, burst.Interval, burst.Probability ) ).ToArray();
		return emitter;
	}
}

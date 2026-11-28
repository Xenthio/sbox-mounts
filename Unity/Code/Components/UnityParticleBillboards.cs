using System.Buffers;

namespace UnityMount;

/// <summary>Draws a mounted particle system with its authored billboard material.</summary>
[Title( "Mounted Particle Billboards" )]
public sealed class UnityParticleBillboards : Component, Component.ExecuteInEditor, Component.DontExecuteOnServer
{
	[Property] public Material Material { get; set; }
	[Property] public bool Opaque { get; set; }
	[Property] public bool Refractive { get; set; }
	[Property] public bool UseSeparateHeight { get; set; }
	[Property] public ParticleFloat Height { get; set; }
	[Property] public bool StretchToVelocity { get; set; }
	[Property] public float LengthScale { get; set; } = 1f;
	[Property] public float VelocityScale { get; set; }
	[Property] public float MaxViewportFraction { get; set; }
	[Property] public Vector2 Pivot { get; set; }
	[Property] public bool FreeformStretch { get; set; }
	[Property] public bool RotateWithStretchDirection { get; set; } = true;

	ParticleEffect _effect;
	BillboardSceneObject _renderer;

	protected override void OnEnabled()
	{
		_effect = GetComponent<ParticleEffect>( true );
		if ( !_effect.IsValid() || Material is null || Material.IsError ) return;
		_renderer = new BillboardSceneObject( Scene.SceneWorld, Material, Opaque, Refractive, MaxViewportFraction );
		_effect.OnPostStep += UpdateBillboards;
	}

	protected override void OnDisabled()
	{
		if ( _effect.IsValid() ) _effect.OnPostStep -= UpdateBillboards;
		_renderer?.Delete();
		_renderer = null;
		_effect = null;
	}

	void UpdateBillboards( float delta )
	{
		var cards = new Billboard[_effect.Particles.Count];
		var bounds = BBox.FromPositionAndSize( WorldPosition, 0 );
		var freeform = StretchToVelocity && FreeformStretch;
		for ( var i = 0; i < cards.Length; i++ )
		{
			var particle = _effect.Particles[i];
			var width = particle.Size.x;
			var height = width;
			if ( UseSeparateHeight && particle.StartScale > 0 )
				height = Height.Evaluate( 0, particle.Rand( 97 ) ) * width / particle.StartScale;
			var velocity = StretchToVelocity ? particle.Velocity : Vector3.Zero;
			var stretchedHeight = StretchToVelocity ? height * LengthScale + velocity.Length * VelocityScale : height;
			var stretch = freeform && MathF.Abs( height ) > 0.0001f ? stretchedHeight / height : 1f;
			if ( !freeform ) height = stretchedHeight;

			var extent = MathF.Abs( width ) * (0.5f + MathF.Abs( Pivot.x )) +
				MathF.Abs( height ) * (0.5f + MathF.Abs( Pivot.y ));
			if ( freeform ) extent *= MathF.Max( 1f, MathF.Abs( stretch ) );
			bounds = bounds.AddBBox( new BBox( particle.Position - extent, particle.Position + extent ) );
			cards[i] = new( particle.Position, new Vector2( width, height ), particle.Angles.roll, velocity, Pivot,
				particle.Color.WithAlpha( particle.Color.a * particle.Alpha ), freeform, RotateWithStretchDirection, stretch );
		}
		_renderer.Bounds = bounds;
		_renderer.Update( cards );
	}

	readonly record struct Billboard( Vector3 Position, Vector2 Size, float Roll, Vector3 Velocity, Vector2 Pivot, Color32 Color,
		bool Freeform, bool RotateWithDirection, float Stretch );

	sealed class BillboardSceneObject : SceneCustomObject
	{
		readonly Material _material;
		readonly RenderAttributes _refractionAttributes;
		readonly float _maxViewportFraction;
		volatile Billboard[] _cards = [];

		public BillboardSceneObject( SceneWorld world, Material material, bool opaque, bool refractive, float maxViewportFraction ) : base( world )
		{
			_material = material;
			_maxViewportFraction = maxViewportFraction;
			Flags.IsOpaque = opaque;
			Flags.IsTranslucent = !opaque;
			Flags.CastShadows = false;
			if ( refractive ) _refractionAttributes = new();
		}

		public void Update( Billboard[] cards ) => _cards = cards;

		public override void RenderSceneObject()
		{
			var cards = _cards;
			if ( cards.Length == 0 ) return;
			if ( Flags.IsOpaque ) Graphics.SetupLighting( this );
			var vertices = ArrayPool<Vertex>.Shared.Rent( cards.Length * 6 );
			try
			{
				var rotation = Graphics.CameraRotation;
				var right = rotation.Right;
				var up = rotation.Up;
				var normal = -rotation.Forward;
				var camera = Graphics.CameraPosition;
				var viewport = _maxViewportFraction > 0f ? ViewportDepth.FromFrustum( Graphics.Frustum, camera, rotation.Forward ) : null;
				var index = 0;
				foreach ( var card in cards )
				{
					var projected = card.Velocity - normal * card.Velocity.Dot( normal );
					var (sin, cos) = MathF.SinCos( card.Roll * (MathF.PI / 180) );
					var hasDirection = !projected.IsNearlyZero();
					var verticalAxis = hasDirection ? projected.Normal : up * cos - right * sin;
					var horizontalAxis = Vector3.Cross( verticalAxis, normal );
					var across = horizontalAxis * (card.Size.x * 0.5f);
					var vertical = verticalAxis * (MathF.Abs( card.Size.y ) * 0.5f);
					var topV = card.Size.y < 0f ? 1f : 0f;
					var bottomV = 1f - topV;
					var reverseWinding = false;
					if ( card.Freeform )
					{
						var stretchAxis = hasDirection ? projected.Normal : up;
						var baseUp = card.RotateWithDirection ? stretchAxis : up;
						var baseRight = Vector3.Cross( baseUp, normal );
						var amount = hasDirection ? card.Stretch - 1f : 0f;
						// Stretch in the view plane after applying the particle's own roll.
						across = Stretch( baseRight * cos + baseUp * sin ) * (card.Size.x * 0.5f);
						vertical = Stretch( baseUp * cos - baseRight * sin ) * (MathF.Abs( card.Size.y ) * 0.5f);
						reverseWinding = amount < -1f;

						Vector3 Stretch( Vector3 axis ) => axis + stretchAxis * (axis.Dot( stretchAxis ) * amount);
					}
					if ( viewport is { } dimensions )
					{
						var depth = (card.Position - camera).Dot( rotation.Forward );
						var limit = dimensions.SizeAt( depth ) * (_maxViewportFraction * 0.5f);
						var extentX = MathF.Abs( across.Dot( right ) ) + MathF.Abs( vertical.Dot( right ) );
						var extentY = MathF.Abs( across.Dot( up ) ) + MathF.Abs( vertical.Dot( up ) );
						var scale = MathF.Min( 1f, MathF.Min( limit.x / MathF.Max( extentX, 0.001f ), limit.y / MathF.Max( extentY, 0.001f ) ) );
						across *= scale;
						vertical *= scale;
					}
					var verticalPivot = card.Size.y < 0f ? -card.Pivot.y : card.Pivot.y;
					var center = card.Position - across * (2 * card.Pivot.x) - vertical * (2 * verticalPivot);
					var topLeft = Corner( center - across + vertical, 0, topV );
					var topRight = Corner( center + across + vertical, 1, topV );
					var bottomRight = Corner( center + across - vertical, 1, bottomV );
					var bottomLeft = Corner( center - across - vertical, 0, bottomV );
					vertices[index++] = topLeft;
					vertices[index++] = reverseWinding ? topRight : bottomRight;
					vertices[index++] = reverseWinding ? bottomRight : topRight;
					vertices[index++] = topLeft;
					vertices[index++] = reverseWinding ? bottomRight : bottomLeft;
					vertices[index++] = reverseWinding ? bottomLeft : bottomRight;

					Vertex Corner( Vector3 position, float u, float v ) =>
						new( position, normal, right, new Vector4( u, v, 0, 0 ) ) { Color = card.Color };
				}
				// Custom billboard draws need their own scene-color binding.
				if ( _refractionAttributes is not null )
					Graphics.GrabFrameTexture( "UnityDistortionScene", _refractionAttributes );
				Graphics.Draw( vertices.AsSpan(), index, _material, _refractionAttributes );
			}
			finally
			{
				ArrayPool<Vertex>.Shared.Return( vertices );
			}
		}
	}

	readonly record struct ViewportDepth( float Near, float Far, Vector2 NearSize, Vector2 FarSize )
	{
		public static ViewportDepth? FromFrustum( Frustum frustum, Vector3 camera, Vector3 forward )
		{
			if ( frustum.GetCorner( 0 ) is not { } nearTopLeft || frustum.GetCorner( 1 ) is not { } nearTopRight ||
				frustum.GetCorner( 3 ) is not { } nearBottomLeft || frustum.GetCorner( 4 ) is not { } farTopLeft ||
				frustum.GetCorner( 5 ) is not { } farTopRight || frustum.GetCorner( 7 ) is not { } farBottomLeft ) return null;

			var near = (nearTopLeft - camera).Dot( forward );
			var far = (farTopLeft - camera).Dot( forward );
			if ( !float.IsFinite( near ) || !float.IsFinite( far ) || far <= near ) return null;
			return new( near, far,
				new( nearTopLeft.Distance( nearTopRight ), nearTopLeft.Distance( nearBottomLeft ) ),
				new( farTopLeft.Distance( farTopRight ), farTopLeft.Distance( farBottomLeft ) ) );
		}

		public Vector2 SizeAt( float depth ) => Vector2.Lerp( NearSize, FarSize, Math.Clamp( (depth - Near) / (Far - Near), 0f, 1f ) );
	}
}

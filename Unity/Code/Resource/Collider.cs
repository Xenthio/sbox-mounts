namespace UnityMount;

readonly record struct UnityPrefabCollider( UnityCollider Source, string ModelPath = null, string SurfacePath = null, UnityAssetPointer? Mesh = null )
{
	public void AddTo( GameObject gameObject )
	{
		Collider collider;
		switch ( Source )
		{
			case UnityCollider.Box shape:
				var box = gameObject.AddComponent<BoxCollider>( false );
				box.Center = shape.Center;
				box.Scale = shape.Size;
				collider = box;
				break;
			case UnityCollider.Sphere shape:
				var sphere = gameObject.AddComponent<SphereCollider>( false );
				sphere.Center = shape.Center;
				sphere.Radius = shape.Radius;
				collider = sphere;
				break;
			case UnityCollider.Capsule shape:
				var capsule = gameObject.AddComponent<CapsuleCollider>( false );
				capsule.Start = shape.Start;
				capsule.End = shape.End;
				capsule.Radius = shape.Radius;
				collider = capsule;
				break;
			case UnityCollider.Mesh shape:
				var model = Model.Load( ModelPath );
				if ( model?.Physics?.Parts.Any( part => shape.Convex ? part.Hulls.Count > 0 : part.Meshes.Count > 0 ) != true ) return;
				var mesh = gameObject.AddComponent<ModelCollider>( false );
				mesh.Model = model;
				collider = mesh;
				break;
			default:
				return;
		}
		collider.IsTrigger = Source.Trigger;
		if ( SurfacePath is not null )
			collider.Surface = GameResource.Load<Surface>( SurfacePath );
		collider.Enabled = Source.Enabled;
	}
}

sealed class UnityCollisionModelLoader( UnityAssetPointer source, bool convex ) : ResourceLoader<GameMount>
{
	protected override object Load()
	{
		using var stream = source.File.OpenBundle();
		var mesh = UnityMesh.Read( source.File.Bundle, stream, source.File.Entry, source.Asset );
		if ( mesh is null ) return null;

		var positions = mesh.Vertices.Select( vertex => vertex.Position ).ToArray();
		if ( !convex )
			return Model.Builder.WithName( Path ).AddCollisionMesh( positions, mesh.Indices.SelectMany( indices => indices ).ToArray() ).Create();

		var model = CookHull( positions );
		if ( model?.Physics?.Parts.Any( part => part.Hulls.Count > 0 ) == true ) return model;

		// Keep the original support vertices and fit the shape within the native face limit.
		return CookHull( SelectHullVertices( positions ) );
	}

	static Vector3[] SelectHullVertices( Vector3[] positions )
	{
		const int directionCount = 120;
		var selected = new HashSet<Vector3>();
		var bounds = new BBox( positions[0], positions[0] );
		foreach ( var position in positions ) bounds = bounds.AddPoint( position );
		var size = bounds.Size;
		var inverseScale = new Vector3( 1 / MathF.Max( size.x, 0.001f ),
			1 / MathF.Max( size.y, 0.001f ), 1 / MathF.Max( size.z, 0.001f ) );

		// Include extremal points so the reduced hull retains the original bounds.
		Support( Vector3.Forward );
		Support( Vector3.Backward );
		Support( Vector3.Left );
		Support( Vector3.Right );
		Support( Vector3.Up );
		Support( Vector3.Down );

		// Uniformly cover the sphere with a Fibonacci lattice. At most 126
		// vertices can produce at most 248 triangular faces.
		var goldenAngle = MathF.PI * (3 - MathF.Sqrt( 5 ));
		for ( var i = 0; i < directionCount; i++ )
		{
			var z = 1 - 2 * (i + 0.5f) / directionCount;
			var r = MathF.Sqrt( 1 - z * z );
			var angle = i * goldenAngle;
			Support( new Vector3( r * MathF.Cos( angle ), r * MathF.Sin( angle ), z ) * inverseScale );
		}
		return selected.ToArray();

		void Support( Vector3 direction )
		{
			var vertex = positions[0];
			var distance = Vector3.Dot( vertex, direction );
			foreach ( var point in positions )
			{
				var candidate = Vector3.Dot( point, direction );
				if ( candidate <= distance ) continue;
				distance = candidate;
				vertex = point;
			}
			selected.Add( vertex );
		}
	}

	Model CookHull( Vector3[] positions )
	{
		if ( positions.Length < 4 ) return null;
		var builder = Model.Builder.WithName( Path );
		builder.AddBody().AddHull( positions, simplify: new PhysicsBodyBuilder.HullSimplify
		{
			Method = PhysicsBodyBuilder.SimplifyMethod.QEM,
			MaxVerts = 255,
			MaxFaces = 255,
			MaxEdges = 768,
			AngleTolerance = 1,
			DistanceTolerance = 0.05f
		} );
		return builder.Create();
	}
}

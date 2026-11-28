namespace UnityMount;

sealed class UnityCompositeModelLoader( UnityCompositeModelLoader.Part[] parts, float[] switchDistances,
	UnityCompositeModelLoader.Collision[] collisions ) : ResourceLoader<GameMount>
{
	public sealed record Part( UnityAssetPointer Mesh, string[] Materials, Transform Local, int[] Levels );
	public sealed record Collision( UnityCollider Shape, Transform Local, UnityAssetPointer? Mesh );

	protected override object Load()
	{
		var builder = Model.Builder.WithName( Path );
		for ( var i = 1; i < switchDistances.Length; i++ )
			builder.WithLodDistance( i, switchDistances[i] );

		var meshes = new Dictionary<UnityAssetPointer, UnityMesh>();
		var hasMesh = false;
		foreach ( var part in parts )
		{
			var source = ReadMesh( part.Mesh );
			if ( source is not { Vertices.Length: > 0 } ) continue;
			var vertices = source.Vertices.Select( vertex => TransformVertex( vertex, part.Local ) ).ToArray();
			var bounds = vertices.Aggregate( new BBox( vertices[0].Position, vertices[0].Position ),
				(box, vertex) => box.AddPoint( vertex.Position ) );

			for ( var i = 0; i < source.Indices.Length; i++ )
			{
				var indices = source.Indices[i];
				if ( indices.Length == 0 ) continue;
				var path = i < part.Materials.Length ? part.Materials[i] : null;
				var mesh = new Mesh( Material.Load( path ?? "materials/dev/reflectivity_30.vmat" ) );
				mesh.CreateVertexBuffer( vertices.Length, vertices );
				mesh.CreateIndexBuffer( indices.Length, TransformIndices( indices, part.Local ) );
				mesh.Bounds = bounds;

				if ( part.Levels.Length == 0 ) builder.AddMesh( mesh );
				else foreach ( var level in part.Levels ) builder.AddMesh( mesh, level );
				hasMesh = true;
			}
		}
		if ( !hasMesh ) return null;

		foreach ( var (shape, local, collisionMesh) in collisions )
		{
			var scale = local.Scale.Abs();
			var maxScale = MathF.Max( scale.x, MathF.Max( scale.y, scale.z ) );
			switch ( shape )
			{
				case UnityCollider.Box box:
					builder.AddCollisionBox( box.Size * scale * .5f, local.PointToWorld( box.Center ), local.Rotation );
					break;
				case UnityCollider.Sphere sphere:
					builder.AddCollisionSphere( sphere.Radius * maxScale, local.PointToWorld( sphere.Center ) );
					break;
				case UnityCollider.Capsule capsule:
					var axis = (capsule.End - capsule.Start).Normal.Abs();
					var radialScale = scale * (Vector3.One - axis);
					var radius = capsule.Radius * MathF.Max( radialScale.x, MathF.Max( radialScale.y, radialScale.z ) );
					builder.AddCollisionCapsule( local.PointToWorld( capsule.Start ), local.PointToWorld( capsule.End ), radius );
					break;
				case UnityCollider.Mesh mesh when collisionMesh is { } pointer && ReadMesh( pointer ) is { } source:
					var points = source.Vertices.Select( vertex => local.PointToWorld( vertex.Position ) ).ToArray();
					if ( mesh.Convex ) builder.AddCollisionHull( points.AsSpan() );
					else builder.AddCollisionMesh( points.AsSpan(), source.Indices.SelectMany( indices => TransformIndices( indices, local ) ).ToArray().AsSpan() );
					break;
			}
		}

		return builder.Create();

		UnityMesh ReadMesh( UnityAssetPointer pointer )
		{
			if ( !meshes.TryGetValue( pointer, out var mesh ) ) meshes.Add( pointer, mesh = UnityMesh.Read( pointer ) );
			return mesh;
		}
	}

	static UnityMeshVertex TransformVertex( UnityMeshVertex vertex, Transform local )
	{
		vertex.Position = local.PointToWorld( vertex.Position );
		vertex.Normal = local.NormalToWorld( vertex.Normal / local.Scale );
		var tangent = local.NormalToWorld( new Vector3( vertex.Tangent.x, vertex.Tangent.y, vertex.Tangent.z ) * local.Scale );
		vertex.Tangent = new( tangent.x, tangent.y, tangent.z, IsMirrored( local ) ? -vertex.Tangent.w : vertex.Tangent.w );
		return vertex;
	}

	static bool IsMirrored( Transform local ) => local.Scale.x * local.Scale.y * local.Scale.z < 0f;

	static int[] TransformIndices( int[] indices, Transform local )
	{
		if ( !IsMirrored( local ) ) return indices;
		var result = indices.ToArray();
		for ( var i = 0; i < result.Length; i += 3 )
			(result[i + 1], result[i + 2]) = (result[i + 2], result[i + 1]);
		return result;
	}
}

namespace UnityMount;

sealed class UnityBillboardModelLoader( UnityBillboard source, string materialPath ) : ResourceLoader<GameMount>
{
	protected override object Load()
	{
		var material = Material.Load( materialPath );
		if ( !material.IsValid() || material.IsError ) return null;

		var builder = Model.Builder.WithName( Path );
		var indices = source.Indices.ToArray();
		// The authored index order faces away from the camera after conversion to Source axes.
		for ( var i = 0; i < indices.Length; i += 3 )
			(indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);

		for ( var i = 0; i < source.Images.Length; i++ )
		{
			var atlas = source.Images[i];
			var vertices = new UnityMeshVertex[source.Vertices.Length];
			for ( var j = 0; j < vertices.Length; j++ )
			{
				var point = source.Vertices[j];
				var uv = atlas.w < 0f
					? new Vector2( atlas.x - atlas.z * point.y, atlas.y - atlas.w * point.x )
					: new Vector2( atlas.x + atlas.z * point.x, atlas.y + atlas.w * point.y );
				vertices[j] = new UnityMeshVertex
				{
					Position = new( 0f, (point.x - 0.5f) * source.Width, source.Bottom + point.y * source.Height ),
					Normal = Vector3.Forward,
					Tangent = new( 0f, 1f, 0f, -1f ),
					TexCoord = new( uv.x, 1f - uv.y ),
					Color = new( 255, 255, 255, 255 )
				};
			}
			var bounds = vertices.Aggregate( new BBox( vertices[0].Position, vertices[0].Position ),
				(box, vertex) => box.AddPoint( vertex.Position ) );
			var mesh = new Mesh( material );
			mesh.CreateVertexBuffer( vertices.Length, vertices );
			mesh.CreateIndexBuffer( indices.Length, indices );
			mesh.Bounds = bounds;
			builder.AddMesh( mesh, UnityBillboardRenderer.AtlasGroup, i );
		}
		return builder.Create();
	}
}

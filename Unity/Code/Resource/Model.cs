using System.Runtime.InteropServices;

namespace UnityMount;

[StructLayout( LayoutKind.Sequential )]
struct UnitySkinnedVertex( UnityMeshVertex vertex, UnityMesh.BoneInfluence influence, byte[] boneIndices )
{
	[VertexLayout.Position] public Vector3 Position = vertex.Position;
	[VertexLayout.Normal] public Vector3 Normal = vertex.Normal;
	[VertexLayout.Tangent] public Vector4 Tangent = vertex.Tangent;
	[VertexLayout.TexCoord( 0 )] public Vector2 TexCoord = vertex.TexCoord;
	[VertexLayout.TexCoord( 1 )] public Vector2 SecondaryTexCoord = vertex.SecondaryTexCoord;
	[VertexLayout.Color] public Color32 Color = vertex.Color;
	[VertexLayout.BlendIndices] public Color32 Indices = new( boneIndices[influence.Indices.r], boneIndices[influence.Indices.g],
		boneIndices[influence.Indices.b], boneIndices[influence.Indices.a] );
	[VertexLayout.BlendWeight] public Color32 Weights = influence.Weights;
}

readonly record struct UnitySurfaceRange( int First, int Count );

sealed class UnityModelLoader( UnityAssetPointer meshSource, string[] materialPaths,
	UnitySkin skin = null, UnityAnimationSource[] animations = null, UnitySurfaceRange? surfaces = null,
	Func<UnityAssetFile, UnityReference, UnityAnimationEventPrefab> findEventPrefab = null, UnityStaticBatchCache staticBatches = null,
	UnityControllerAnimations controller = null, IReadOnlyDictionary<string, string> inPlaceAnimations = null ) : ResourceLoader<GameMount>
{
	protected override object Load()
	{
		var source = staticBatches is null ? UnityMesh.Read( meshSource, skin ) : staticBatches.Read( meshSource );
		if ( source is null ) return null;

		var vertices = source.Vertices;
		var indices = source.Indices;
		if ( surfaces is { } batch )
		{
			if ( batch.First + batch.Count > indices.Length ) return null;
			(vertices, indices) = ExtractSurfaces( vertices, indices.AsSpan( batch.First, batch.Count ) );
			if ( vertices.Length == 0 ) return null;
		}

		var builder = Model.Builder.WithName( Path );
		UnitySkinnedVertex[] skinned = null;
		byte[] boneIndices = null;
		Transform[] modelBindPose = null;
		if ( skin is not null )
		{
			if ( !skin.TryBuild( source.BindPoses, source.BoneBounds, builder, out boneIndices, out var bindTransforms,
				out var parentScales, out var affine, out modelBindPose ) ) return null;
			skinned = new UnitySkinnedVertex[source.Vertices.Length];
			for ( var i = 0; i < skinned.Length; i++ )
				skinned[i] = new( source.Vertices[i], source.Influences[i], boneIndices );
			skin.AddAnimations( builder, animations ?? [], boneIndices, bindTransforms, parentScales, affine, findEventPrefab, controller, inPlaceAnimations );
		}

		var bounds = new BBox( vertices[0].Position, vertices[0].Position );
		foreach ( var vertex in vertices ) bounds = bounds.AddPoint( vertex.Position );
		if ( skin?.ViewBounds is { } authoredBounds ) builder.WithViewBounds( bounds.AddBBox( authoredBounds ) );

		for ( var i = 0; i < indices.Length; i++ )
		{
			if ( indices[i].Length == 0 ) continue;
			var materialPath = i < materialPaths.Length ? materialPaths[i] : null;
			var material = Material.Load( materialPath ?? "materials/dev/reflectivity_30.vmat" );
			var mesh = new Mesh( material );
			if ( skinned is null ) mesh.CreateVertexBuffer( vertices.Length, vertices );
			else mesh.CreateVertexBuffer( skinned.Length, skinned );
			mesh.CreateIndexBuffer( indices[i].Length, indices[i] );
			mesh.Bounds = bounds;
			builder.AddMesh( mesh );
		}
		var model = builder.Create();
		if ( skin is not null && model.IsValid() )
			UnityParticleSurface.Register( model, meshSource, skin, boneIndices, modelBindPose );
		return model;
	}

	static (UnityMeshVertex[] Vertices, int[][] Indices) ExtractSurfaces( UnityMeshVertex[] source, ReadOnlySpan<int[]> surfaces )
	{
		var remap = new Dictionary<int, int>();
		var vertices = new List<UnityMeshVertex>();
		var indices = new int[surfaces.Length][];
		for ( var i = 0; i < surfaces.Length; i++ )
		{
			var surface = surfaces[i];
			var output = new int[surface.Length];
			for ( var j = 0; j < surface.Length; j++ )
			{
				var index = surface[j];
				if ( !remap.TryGetValue( index, out var mapped ) )
				{
					remap.Add( index, mapped = vertices.Count );
					vertices.Add( source[index] );
				}
				output[j] = mapped;
			}
			indices[i] = output;
		}
		return (vertices.ToArray(), indices);
	}
}

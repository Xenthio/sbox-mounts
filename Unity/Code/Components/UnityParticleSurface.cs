using System.Runtime.CompilerServices;

namespace UnityMount;

/// <summary>Samples the animated surface of a mounted skinned model for particle emission.</summary>
sealed class UnityParticleSurface( ModelRenderer renderer )
{
	readonly record struct Triangle( int A, int B, int C, float AreaEnd );
	sealed record Geometry( UnityMesh Mesh, byte[] BoneIndices, Transform[] BindPose, Triangle[] Triangles );

	static readonly ConditionalWeakTable<Model, Lazy<Geometry>> Surfaces = new();

	public static void Register( Model model, UnityAssetPointer source, UnitySkin skin, byte[] boneIndices, Transform[] bindPose )
	{
		// Only load CPU geometry for models that are actually used by a particle emitter.
		Surfaces.GetValue( model, _ => new( () => Build( UnityMesh.Read( source, skin ), boneIndices, bindPose ) ) );
	}

	static Geometry Build( UnityMesh mesh, byte[] boneIndices, Transform[] bindPose )
	{
		if ( mesh is null ) return null;

		var triangles = new List<Triangle>();
		var area = 0f;
		foreach ( var indices in mesh.Indices )
		{
			for ( var i = 0; i + 2 < indices.Length; i += 3 )
			{
				var a = indices[i];
				var b = indices[i + 1];
				var c = indices[i + 2];
				var ab = mesh.Vertices[b].Position - mesh.Vertices[a].Position;
				var ac = mesh.Vertices[c].Position - mesh.Vertices[a].Position;
				var next = area + Vector3.Cross( ab, ac ).Length;
				if ( !float.IsFinite( next ) || next <= area ) continue;
				area = next;
				triangles.Add( new( a, b, c, area ) );
			}
		}
		return triangles.Count > 0 ? new( mesh, boneIndices, bindPose, triangles.ToArray() ) : null;
	}

	public bool TrySample( out Vector3 position, out Vector3 normal )
	{
		position = default;
		normal = Vector3.Up;
		if ( !renderer.IsValid() || renderer.Model is not { IsError: false } model ) return false;

		if ( renderer is SkinnedModelRenderer skinned && skinned.SceneModel.IsValid() &&
			Surfaces.TryGetValue( model, out var surface ) && surface.Value is { } geometry )
		{
			var triangles = geometry.Triangles;
			var area = Game.Random.Float( 0f, triangles[^1].AreaEnd );
			var first = 0;
			var last = triangles.Length - 1;
			while ( first < last )
			{
				var middle = (first + last) / 2;
				if ( triangles[middle].AreaEnd < area ) first = middle + 1;
				else last = middle;
			}

			var triangle = triangles[first];
			var a = Skin( triangle.A, geometry, skinned.SceneModel );
			var b = Skin( triangle.B, geometry, skinned.SceneModel );
			var c = Skin( triangle.C, geometry, skinned.SceneModel );
			var u = MathF.Sqrt( Game.Random.Float() );
			var v = Game.Random.Float();
			position = a * (1f - u) + b * (u * (1f - v)) + c * (u * v);
			normal = Vector3.Cross( b - a, c - a ).Normal;
			return true;
		}

		// Native s&box models do not expose their skin weights through the game API.
		var transform = renderer.WorldTransform;
		if ( model.HitboxSet?.All is { Count: > 0 } hitboxes )
		{
			var box = hitboxes[Game.Random.Int( 0, hitboxes.Count - 1 )];
			if ( renderer is SkinnedModelRenderer target && box.Bone is not null &&
				target.TryGetBoneTransform( box.Bone, out var bone ) )
				transform = bone;
			position = transform.PointToWorld( box.RandomPointOnEdge );
		}
		else
			position = transform.PointToWorld( model.Bounds.RandomPointOnEdge );
		return true;
	}

	static Vector3 Skin( int index, Geometry geometry, SceneModel model )
	{
		var vertex = geometry.Mesh.Vertices[index].Position;
		var influence = geometry.Mesh.Influences[index];
		var position = Vector3.Zero;
		Add( influence.Indices.r, influence.Weights.r );
		Add( influence.Indices.g, influence.Weights.g );
		Add( influence.Indices.b, influence.Weights.b );
		Add( influence.Indices.a, influence.Weights.a );
		return position;

		void Add( byte index, byte weight )
		{
			if ( weight == 0 ) return;
			var bone = geometry.BoneIndices[index];
			var local = geometry.BindPose[bone].PointToLocal( vertex );
			position += model.GetBoneWorldTransform( bone ).PointToWorld( local ) * (weight / 255f);
		}
	}
}

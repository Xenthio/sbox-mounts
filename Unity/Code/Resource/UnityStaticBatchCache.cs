using System.Runtime.CompilerServices;

namespace UnityMount;

sealed class UnityStaticBatchCache
{
	const long MaxBytes = 192L * 1024 * 1024;

	readonly object _lock = new();
	Dictionary<UnityAssetPointer, UnityMesh> _meshes;
	long _bytes;

	public void Begin()
	{
		lock ( _lock )
		{
			_meshes = new();
			_bytes = 0;
		}
	}

	public void End()
	{
		lock ( _lock )
		{
			_meshes = null;
			_bytes = 0;
		}
	}

	public UnityMesh Read( UnityAssetPointer source )
	{
		lock ( _lock )
		{
			if ( _meshes is not null )
			{
				if ( _meshes.TryGetValue( source, out var cached ) ) return cached;

				var mesh = UnityMesh.Read( source );
				var size = mesh is null ? 0 : (long)mesh.Vertices.Length * Unsafe.SizeOf<UnityMeshVertex>() +
					mesh.Indices.Sum( indices => (long)indices.Length * sizeof( int ) );
				if ( size <= MaxBytes - _bytes )
				{
					_meshes.Add( source, mesh );
					_bytes += size;
				}
				return mesh;
			}
		}
		return UnityMesh.Read( source );
	}
}

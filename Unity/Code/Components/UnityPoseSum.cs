namespace UnityMount;

struct UnityPoseSum
{
	public Vector3 Position;
	public Vector3 Scale;
	public Vector4 Rotation;

	public void Add( Transform transform, float weight )
	{
		var rotation = transform.Rotation;
		var q = new Vector4( rotation.x, rotation.y, rotation.z, rotation.w );
		if ( Vector4.Dot( Rotation, q ) < 0 ) q = -q;
		Rotation += q * weight;
		Position += transform.Position * weight;
		Scale += transform.Scale * weight;
	}

	public readonly Transform Resolve() =>
		new( Position, new Rotation( Rotation.x, Rotation.y, Rotation.z, Rotation.w ).Normal, Scale );
}

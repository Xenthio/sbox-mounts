namespace UnityMount;

/// <summary>The authored additive reference pose in native parent-space bone order.</summary>
[Sandbox.ModelEditor.GameData( "unity_additive_reference_pose", AllowMultiple = true ), Hide]
public sealed record UnityAdditivePoseData
{
	public string Sequence { get; init; }
	public Transform[] Bones { get; init; } = [];

	internal static Transform Delta( Transform reference, Transform value )
	{
		var scale = reference.Scale;
		return new( value.Position - reference.Position, reference.Rotation.Inverse * value.Rotation,
			new Vector3( Ratio( value.Scale.x, scale.x ), Ratio( value.Scale.y, scale.y ), Ratio( value.Scale.z, scale.z ) ) );

		static float Ratio( float amount, float basis ) => basis == 0f ? 1f : amount / basis;
	}

	internal static Transform Apply( Transform basis, Transform delta, float weight ) =>
		new( basis.Position + delta.Position * weight,
			basis.Rotation * Rotation.Slerp( Rotation.Identity, delta.Rotation, weight ),
			basis.Scale * Vector3.Lerp( Vector3.One, delta.Scale, weight ) );
}

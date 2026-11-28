namespace UnityMount;

static class UnityCoordinates
{
	const float InchesPerMeter = 39.37008f;

	// Unity's right, up and forward axes become -Y, Z and X in Source coordinates.
	public static Vector3 Direction( System.Numerics.Vector3 value ) => new( value.Z, -value.X, value.Y );
	public static Vector3 Position( System.Numerics.Vector3 value ) => ToInches( Direction( value ) );
	public static Rotation RotationFor( System.Numerics.Quaternion rotation ) => Rotation.LookAt(
		Direction( System.Numerics.Vector3.Transform( System.Numerics.Vector3.UnitZ, rotation ) ),
		Direction( System.Numerics.Vector3.Transform( System.Numerics.Vector3.UnitY, rotation ) ) );

	public static Rotation EulerRotation( System.Numerics.Vector3 degrees )
	{
		var radians = degrees * (MathF.PI / 180);
		var x = System.Numerics.Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitX, radians.X );
		var y = System.Numerics.Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitY, radians.Y );
		var z = System.Numerics.Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitZ, radians.Z );
		// Unity applies Euler rotations in Z, X, Y order.
		return RotationFor( y * x * z );
	}

	public static Vector3 Scale( System.Numerics.Vector3 value ) => new( value.Z, value.X, value.Y );
	public static float ToInches( float value ) => value * InchesPerMeter;
	public static Vector3 ToInches( Vector3 value ) => value * InchesPerMeter;
}

namespace UnityMount;

static class UnityColor
{
	public static Color ToLinear( Color color ) => new( ToLinear( color.r ), ToLinear( color.g ), ToLinear( color.b ), color.a );

	public static float ToLinear( float value ) => value <= 0.04045f ? Math.Max( 0, value ) / 12.92f
		: value < 1 ? MathF.Pow( (value + 0.055f) / 1.055f, 2.4f ) : MathF.Pow( value, 2.2f );
}

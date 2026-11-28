namespace UnityMount;

sealed record UnityParticleNoiseSettings( ParticleVector3 Strength, float Frequency, bool Damping, int Quality,
	int Octaves, float OctaveMultiplier, float OctaveScale, ParticleFloat ScrollSpeed, ParticleVector3? Remap,
	ParticleFloat PositionAmount, ParticleFloat RotationAmount, ParticleFloat SizeAmount )
{
	public static UnityParticleNoiseSettings Read( UnityReader reader, UnityTypeTree tree, long start )
	{
		var separateAxes = ReadBool( "separateAxes" );
		return new( ReadAxes( "strength" ), MathF.Max( 0, ReadScalar( "frequency" ) ), ReadBool( "damping" ),
			Math.Clamp( ReadInt( "quality" ), 0, 2 ), Math.Clamp( ReadInt( "octaves" ), 1, 4 ),
			MathF.Max( 0, ReadScalar( "octaveMultiplier" ) ), MathF.Max( 0, ReadScalar( "octaveScale" ) ),
			ReadCurve( "scrollSpeed" ), ReadBool( "remapEnabled" ) ? ReadAxes( "remap" ) : null,
			ReadCurve( "positionAmount", 1 ), ReadCurve( "rotationAmount" ), ReadCurve( "sizeAmount" ) );

		ParticleVector3 ReadAxes( string field )
		{
			var x = ReadCurve( field );
			// Unity's X, Y, Z map to s&box's left, up, forward.
			return new()
			{
				X = separateAxes ? ReadCurve( field + "Z" ) : x,
				Y = x,
				Z = separateAxes ? ReadCurve( field + "Y" ) : x
			};
		}

		UnityTypeTree Field( string name )
		{
			reader.Position = start;
			return tree.SeekField( reader, name );
		}

		ParticleFloat ReadCurve( string name, float fallback = 0 ) =>
			tree.HasField( name ) ? UnityEffectValues.ReadFloat( reader, Field( name ) ) : fallback;

		bool ReadBool( string name )
		{
			Field( name );
			return reader.ReadBoolean();
		}

		int ReadInt( string name )
		{
			Field( name );
			return reader.ReadInt32();
		}

		float ReadScalar( string name )
		{
			Field( name );
			var value = reader.ReadSingle();
			if ( !float.IsFinite( value ) ) throw new InvalidDataException( "Invalid Unity particle noise value." );
			return value;
		}
	}
}

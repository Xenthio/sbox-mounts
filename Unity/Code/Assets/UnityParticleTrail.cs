namespace UnityMount;

sealed record UnityParticleTrail( float Ratio, float PointDistance, bool WorldSpace, bool SizeAffectsWidth, bool InheritColor,
	ParticleFloat Lifetime, ParticleFloat Width, ParticleGradient ColorOverLifetime, ParticleGradient ColorOverTrail,
	int RibbonCount, bool AttachRibbonToTransform, bool RepeatPerSegment )
{
	public static UnityParticleTrail Read( UnityReader reader, UnityTypeTree tree, long start )
	{
		var mode = ReadInt( "mode" );
		var textureMode = ReadInt( "textureMode" );
		if ( mode is not (0 or 1) || (mode == 0 && (textureMode != 0 || !ReadBool( "dieWithParticles" ) ||
			ReadBool( "sizeAffectsLifetime" ) || ReadBool( "generateLightingData" ))) ) return null;
		var ribbons = mode == 1 ? ReadInt( "ribbonCount" ) : 0;
		if ( mode == 1 && (ribbons <= 0 || textureMode is not (0 or 3) || ReadBool( "splitSubEmitterRibbons" )) ) return null;
		var attach = mode == 1 && tree.HasField( "attachRibbonsToTransform" ) && ReadBool( "attachRibbonsToTransform" );

		var ratio = ReadScalar( "ratio" );
		var distance = UnityCoordinates.ToInches( ReadScalar( "minVertexDistance" ) );
		var lifetimeColor = ReadColor( "colorOverLifetime" );
		var trailColor = ReadColor( "colorOverTrail" );
		if ( !float.IsFinite( ratio ) || ratio <= 0 || ratio > 1 || !float.IsFinite( distance ) || distance < 0 ||
			lifetimeColor is null || trailColor is null ) return null;

		return new( ratio, distance, ReadBool( "worldSpace" ), ReadBool( "sizeAffectsWidth" ), ReadBool( "inheritParticleColor" ),
			UnityEffectValues.ReadFloat( reader, Field( "lifetime" ) ), UnityEffectValues.ReadFloat( reader, Field( "widthOverTrail" ) ),
			lifetimeColor.Value, trailColor.Value, ribbons, attach, textureMode == 3 );

		UnityTypeTree Field( string name )
		{
			reader.Position = start;
			return tree.SeekField( reader, name );
		}

		int ReadInt( string name ) { Field( name ); return reader.ReadInt32(); }
		float ReadScalar( string name ) { Field( name ); return reader.ReadSingle(); }
		bool ReadBool( string name ) { Field( name ); return reader.ReadBoolean(); }

		ParticleGradient? ReadColor( string name )
		{
			var field = Field( name );
			var offset = reader.Position;
			field.SeekField( reader, "minMaxState" );
			var mode = reader.ReadInt16();
			reader.Position = offset;
			if ( mode == 0 )
			{
				field.SeekField( reader, "maxColor" );
				return (ParticleGradient)UnityEffectValues.ReadColor( reader );
			}
			if ( mode == 1 )
				return new ParticleGradient { Type = ParticleGradient.ValueType.Gradient,
					GradientA = UnityEffectValues.ReadGradient( reader, field.SeekField( reader, "maxGradient" ) ) };
			return null;
		}
	}
}

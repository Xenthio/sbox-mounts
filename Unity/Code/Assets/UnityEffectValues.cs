namespace UnityMount;

static class UnityEffectValues
{
	public static ParticleFloat ReadFloat( UnityReader reader, UnityTypeTree tree, float multiplier = 1, bool allowInfiniteLifetime = false )
	{
		var start = reader.Position;
		tree.SeekField( reader, "minMaxState" );
		var mode = reader.ReadInt16();
		reader.Position = start;
		tree.SeekField( reader, "scalar" );
		var high = reader.ReadSingle() * multiplier;
		var low = reader.ReadSingle() * multiplier;
		// The native particle lifetime must round-trip through scene JSON.
		if ( allowInfiniteLifetime && mode == 0 && float.IsPositiveInfinity( high ) ) return float.MaxValue;
		if ( !float.IsFinite( high ) || !float.IsFinite( low ) )
			throw new InvalidDataException( "Invalid Unity particle float." );

		if ( mode == 0 ) return high;
		if ( mode == 3 )
			return new() { Type = ParticleFloat.ValueType.Range, Evaluation = ParticleFloat.EvaluationType.Seed, ConstantA = low, ConstantB = high };
		if ( mode is not (1 or 2) ) throw new InvalidDataException( "Invalid Unity particle float mode." );

		reader.Position = start;
		var maxCurve = ReadCurve( reader, tree.SeekField( reader, "maxCurve" ), high );
		if ( mode == 1 ) return new() { Type = ParticleFloat.ValueType.Curve, CurveA = maxCurve };

		reader.Position = start;
		var minCurve = ReadCurve( reader, tree.SeekField( reader, "minCurve" ), low );
		return new() { Type = ParticleFloat.ValueType.CurveRange, CurveA = minCurve, CurveB = maxCurve };
	}

	public static Curve ReadCurve( UnityReader reader, UnityTypeTree tree, float multiplier = 1 )
	{
		var frames = new List<Curve.Frame>();
		tree.SeekField( reader, "m_Curve" ).VisitArray( reader, (item, _) =>
		{
			item.SeekField( reader, "time" );
			var time = reader.ReadSingle();
			var value = reader.ReadSingle() * multiplier;
			var incoming = reader.ReadSingle() * multiplier;
			var outgoing = reader.ReadSingle() * multiplier;
			if ( !float.IsFinite( time ) || !float.IsFinite( value ) ) throw new InvalidDataException( "Invalid Unity effect curve." );
			frames.Add( new( time, value, float.IsFinite( incoming ) ? -incoming : 0, float.IsFinite( outgoing ) ? outgoing : 0 )
			{
				Mode = float.IsFinite( incoming ) && float.IsFinite( outgoing ) ? Curve.HandleMode.Split : Curve.HandleMode.Stepped
			} );
		} );
		return new( frames.ToArray() );
	}

	public static Color ReadColor( UnityReader reader ) => new( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );

	public static Gradient ReadGradient( UnityReader reader, UnityTypeTree tree )
	{
		var start = reader.Position;
		tree.SeekField( reader, "m_Mode" );
		var mode = reader.ReadInt32();
		if ( mode is not (0 or 1) ) throw new NotSupportedException( "Unsupported Unity gradient blend mode." );
		var colorCount = reader.ReadByte();
		var alphaCount = reader.ReadByte();
		if ( colorCount > 8 || alphaCount > 8 ) throw new InvalidDataException( "Invalid Unity gradient key count." );
		reader.Position = start;
		tree.SeekField( reader, "key0" );
		var keys = new Color[8];
		for ( var i = 0; i < keys.Length; i++ ) keys[i] = ReadColor( reader );
		var gradient = new Gradient { Blending = mode == 0 ? Gradient.BlendMode.Linear : Gradient.BlendMode.Stepped };
		for ( var i = 0; i < 8; i++ )
		{
			var time = reader.ReadUInt16() / 65535f;
			if ( i < colorCount ) gradient.AddColor( time, keys[i].WithAlpha( 1 ) );
		}
		for ( var i = 0; i < 8; i++ )
		{
			var time = reader.ReadUInt16() / 65535f;
			if ( i < alphaCount ) gradient.AddAlpha( time, keys[i].a );
		}
		return gradient;
	}
}

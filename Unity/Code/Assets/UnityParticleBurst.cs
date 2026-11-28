namespace UnityMount;

readonly record struct UnityParticleBurst( float Time, ParticleFloat Count, int Cycles, float Interval, float Probability )
{
	public static UnityParticleBurst[] Read( UnityReader reader, UnityTypeTree tree, int count )
	{
		var bursts = new List<UnityParticleBurst>();
		tree.VisitArray( reader, (item, start) =>
		{
			var time = ReadFloat( "time" );
			var interval = ReadFloat( "repeatInterval" );
			var probability = ReadFloat( "probability" );
			reader.Position = start;
			item.SeekField( reader, "cycleCount" );
			var cycles = reader.ReadInt32();
			if ( time < 0 || interval < 0 || probability is < 0 or > 1 || cycles < 0 || (cycles != 1 && interval <= 0) )
				throw new InvalidDataException( "Invalid Unity particle burst." );
			reader.Position = start;
			var particles = UnityEffectValues.ReadFloat( reader, item.SeekField( reader, "countCurve" ) );
			bursts.Add( new( time, particles, cycles, interval, probability ) );

			float ReadFloat( string name )
			{
				reader.Position = start;
				item.SeekField( reader, name );
				var value = reader.ReadSingle();
				if ( !float.IsFinite( value ) ) throw new InvalidDataException( "Invalid Unity particle burst value." );
				return value;
			}
		} );
		if ( bursts.Count != count ) throw new InvalidDataException( "Invalid Unity particle burst count." );
		return bursts.ToArray();
	}
}

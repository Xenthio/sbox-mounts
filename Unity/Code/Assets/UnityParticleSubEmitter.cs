namespace UnityMount;

readonly record struct UnityParticleSubEmitter( UnityReference Emitter, float Probability, bool InheritRotation )
{
	public static UnityParticleSubEmitter[] Read( UnityReader reader, UnityTypeTree module, long start )
	{
		const int collision = 1;
		const int inheritRotation = 1 << 2;
		var emitters = new List<UnityParticleSubEmitter>();
		reader.Position = start;
		module.SeekField( reader, "subEmitters" ).VisitArray( reader, (entry, position) =>
		{
			entry.SeekField( reader, "emitter" );
			var target = UnityReference.Read( reader );
			reader.Position = position;
			entry.SeekField( reader, "type" );
			var type = reader.ReadInt32();
			var flags = reader.ReadInt32();
			var probability = reader.ReadSingle();
			if ( type == collision && target.Id != 0 && flags is 0 or inheritRotation && probability is > 0 and <= 1 )
				emitters.Add( new( target, probability, flags == inheritRotation ) );
		} );
		return emitters.ToArray();
	}
}

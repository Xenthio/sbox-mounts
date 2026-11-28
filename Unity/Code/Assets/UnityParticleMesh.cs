namespace UnityMount;

sealed record UnityParticleMesh( UnityParticleMesh.Choice[] Choices, UnityParticleMeshes.RenderAlignment Alignment, Vector3 Pivot, Vector3 Flip )
{
	public readonly record struct Choice( UnityReference Mesh, float Weight );

	public static UnityParticleMesh Read( UnityReader reader, UnityTypeTree tree )
	{
		var distribution = 0;
		if ( tree.HasField( "m_MeshDistribution" ) )
		{
			Seek( "m_MeshDistribution" );
			distribution = reader.ReadInt32();
		}
		if ( distribution is not (0 or 1) ) return null;

		var choices = new List<Choice>( 4 );
		for ( var i = 0; i < 4; i++ )
		{
			var suffix = i == 0 ? "" : i.ToString();
			var mesh = ReadReference( $"m_Mesh{suffix}" );
			if ( mesh.Id == 0 ) continue;
			var weight = 1f;
			if ( distribution == 1 )
			{
				Seek( $"m_MeshWeighting{suffix}" );
				weight = reader.ReadSingle();
				if ( !float.IsFinite( weight ) || weight < 0 ) throw new InvalidDataException( "Invalid Unity particle mesh weight." );
			}
			choices.Add( new( mesh, weight ) );
		}
		if ( choices.Count == 0 || !choices.Any( choice => choice.Weight > 0 ) ) return null;

		Seek( "m_RenderAlignment" );
		var alignment = (UnityParticleMeshes.RenderAlignment)reader.ReadInt32();
		if ( alignment is < UnityParticleMeshes.RenderAlignment.View or > UnityParticleMeshes.RenderAlignment.Velocity ) return null;
		return new( choices.ToArray(), alignment, UnityCoordinates.Direction( ReadVector( "m_Pivot" ) ), UnityCoordinates.Scale( ReadVector( "m_Flip" ) ) );

		void Seek( string name )
		{
			reader.Position = 0;
			tree.SeekField( reader, name );
		}

		UnityReference ReadReference( string name )
		{
			Seek( name );
			return UnityReference.Read( reader );
		}

		System.Numerics.Vector3 ReadVector( string name )
		{
			Seek( name );
			var value = new System.Numerics.Vector3( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
			if ( !float.IsFinite( value.LengthSquared() ) ) throw new InvalidDataException( "Invalid Unity particle mesh vector." );
			return value;
		}
	}
}

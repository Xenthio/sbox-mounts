namespace UnityMount;

sealed record UnityShader( string Name, string RenderType )
{
	public const int ClassId = 48;

	public static UnityShader Read( UnityReader reader, UnityTypeTree tree )
	{
		var parsed = tree.SeekField( reader, "m_ParsedForm" );
		var start = reader.Position;
		var subShaders = parsed.SeekField( reader, "m_SubShaders" );
		string renderType = null;
		subShaders.VisitArray( reader, (subShader, _) =>
		{
			if ( renderType is not null ) return;
			var tags = subShader.SeekField( reader, "m_Tags" ).SeekField( reader, "tags" );
			renderType = ReadRenderTypeTag( reader, tags );
		} );
		reader.Position = start;
		parsed.SeekField( reader, "m_Name" );
		return new( reader.ReadAlignedString(), renderType );
	}

	public static string ReadRenderTypeTag( UnityReader reader, UnityTypeTree tags )
	{
		string renderType = null;
		tags.VisitArray( reader, (_, _) =>
		{
			var key = reader.ReadAlignedString();
			var value = reader.ReadAlignedString();
			if ( key == "RenderType" ) renderType = value;
		} );
		return renderType;
	}
}

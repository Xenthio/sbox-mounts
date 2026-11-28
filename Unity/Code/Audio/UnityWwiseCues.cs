namespace UnityMount;

static class UnityWwiseCues
{
	const int StartTrigger = 1281810935;
	const int EnableTrigger = -320808462;

	public readonly record struct Trigger( uint EventId, long? OnceId );
	public readonly record struct Playable( uint EventId, bool StopAtEnd );

	public static Trigger? ReadActivation( BundleIndex bundles, UnityAssetFile file, SerializedFile.Object asset, UnityReader reader )
	{
		var tree = asset.Tree;
		reader.Position = 0;
		tree.SeekField( reader, "enableActionOnEvent" );
		if ( reader.ReadBoolean() ) return null;
		reader.Position = 0;
		tree.SeekField( reader, "useOtherObject" );
		if ( reader.ReadBoolean() ) return null;
		reader.Position = 0;
		tree.SeekField( reader, "soundEmitterObject" );
		if ( UnityReference.Read( reader ).Id != 0 ) return null;

		var start = false;
		var enable = false;
		reader.Position = 0;
		tree.SeekField( reader, "triggerList" ).VisitArray( reader, ( _, _ ) =>
		{
			var trigger = reader.ReadInt32();
			start |= trigger == StartTrigger;
			enable |= trigger == EnableTrigger;
		} );
		if ( !start && !enable ) return null;
		if ( ReadEventId( bundles, file, reader, tree, "data" ) is not { } eventId ) return null;
		return new( eventId, enable ? null : asset.Id );
	}

	public static Playable? ReadTimeline( BundleIndex bundles, UnityAssetFile file, UnityReader reader, UnityTypeTree tree )
	{
		if ( !tree.HasField( "akEvent" ) || !tree.HasField( "retriggerEvent" ) || !tree.HasField( "StopEventAtClipEnd" ) ) return null;
		tree.SeekField( reader, "retriggerEvent" );
		if ( reader.ReadBoolean() ) return null;
		reader.Position = 0;
		tree.SeekField( reader, "StopEventAtClipEnd" );
		var stop = reader.ReadBoolean();
		return ReadEventId( bundles, file, reader, tree, "akEvent" ) is { } id ? new( id, stop ) : null;
	}

	static uint? ReadEventId( BundleIndex bundles, UnityAssetFile file, UnityReader reader, UnityTypeTree tree, string field )
	{
		reader.Position = 0;
		var value = tree.SeekField( reader, field );
		if ( !value.HasField( "WwiseObjectReference" ) ) return null;
		value.SeekField( reader, "WwiseObjectReference" );
		var reference = UnityReference.Read( reader );
		if ( !bundles.TryResolve( file, reference, SerializedFile.MonoBehaviourClassId, out var owner, out var asset ) ||
			asset.Tree?.HasField( "id" ) != true || !asset.Tree.HasField( "objectName" ) || asset.Size > 4096 ) return null;
		using var metadata = new UnityReader( owner.ReadObject( asset, asset.Size ), leaveOpen: false );
		asset.Tree.SeekField( metadata, "id" );
		var id = metadata.ReadUInt32();
		return id != 0 ? id : null;
	}
}

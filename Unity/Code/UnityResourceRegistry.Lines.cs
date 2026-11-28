namespace UnityMount;

sealed partial class UnityResourceRegistry
{
	UnityPrefabLine ReadLine( UnityAssetFile file, SerializedFile.Object asset )
	{
		try
		{
			using var reader = ReadMetadata( file, asset );
			if ( UnityLine.Read( reader, asset.Tree ) is not { } source ) return null;
			var materials = new List<string>();
			foreach ( var reference in source.Materials )
				if ( bundles.TryResolve( file, reference, UnityMaterial.ClassId, out var owner, out var material ) &&
					RegisterMaterial( owner, material ).Path is { } path )
					materials.Add( path );
			return materials.Count > 0 ? new( source, materials.ToArray() ) : null;
		}
		catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
		{
			Log.Warning( $"Could not index Unity line renderer {asset.Id} in '{file.Entry.Name}': {e.Message}" );
			return null;
		}
	}
}

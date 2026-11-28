namespace UnityMount;

sealed partial class UnityResourceRegistry
{
	UnitySceneProbe? RegisterReflectionProbe( UnityAssetFile file, SerializedFile.Object asset )
	{
		try
		{
			using var reader = ReadMetadata( file, asset );
			return UnityReflectionProbe.Read( reader, asset.Tree ) is { } probe
				? RegisterReflectionProbe( file, probe ) : null;
		}
		catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
		{
			Log.Warning( $"Could not index Unity reflection probe {asset.Id} in '{file.Entry.Name}': {e.Message}" );
			return null;
		}
	}

	UnitySceneProbe? RegisterEnvironmentReflection( UnityAssetFile file, UnityEnvironmentReflection reflection )
	{
		// Match the bounds and low priority of s&box's sky environment probe.
		var probe = new UnityReflectionProbe( true, BBox.FromPositionAndSize( Vector3.Zero, int.MaxValue ),
			false, reflection.Intensity, 0, -5, reflection.Cubemap );
		return RegisterReflectionProbe( file, probe );
	}

	UnitySceneProbe? RegisterReflectionProbe( UnityAssetFile file, UnityReflectionProbe probe )
	{
		if ( !bundles.TryResolve( file, probe.Cubemap, UnityTexture.CubemapClassId, out var cubeFile, out var cube ) ||
			RegisterTexture( cubeFile, cube ) is not { } path )
			return null;
		return new( probe, path );
	}
}

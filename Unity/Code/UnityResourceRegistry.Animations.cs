namespace UnityMount;

sealed partial class UnityResourceRegistry
{
	void RegisterLightClip( UnityAssetFile file, SerializedFile.Object asset )
	{
		if ( file.ReadLightAnimation( asset ) is not { } clip ) return;
		var name = SanitizeResourceName( clip.Name );
		if ( !IsResourceName( name ) ) return;
		var path = ReservePath( $"animations/{file.Group}/{name}", ".unitylight", asset.Id );
		if ( path is not null )
			Define( ResourceType.GameResource, path, () => new UnityLightClipLoader( clip ) );
	}
}

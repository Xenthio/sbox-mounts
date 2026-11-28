namespace UnityMount;

public abstract class GameMount : BaseGameMount
{
	protected abstract long AppId { get; }
	protected abstract string BundleDirectory { get; }
	protected virtual string BuiltinResourceDirectory => null;
	protected abstract IReadOnlyList<string> BundleGroups { get; }
	protected virtual IReadOnlyList<string> SceneGroups => [];
	protected virtual IReadOnlyList<string> SpeedTreeGroups => [];
	protected virtual float ParticleGravity => 9.81f;

	private protected virtual UnityVisualOverrides ReadVisualOverrides( BundleIndex bundles, UnityAssetFile[] files ) => new();
	private protected virtual UnitySceneMarkers ReadSceneMarkers( BundleIndex bundles, UnityAssetFile scene ) => new( [], [], [] );

	public override long? SteamAppId => AppId;
	internal BaseFileSystem FileSystem { get; private set; }

	protected override void Initialize( InitializeContext context )
	{
		if ( !context.IsAppInstalled( AppId ) ) return;
		FileSystem = context.GetFileSystem( AppId );
		IsInstalled = FileSystem?.DirectoryExists( BundleDirectory ) == true;
	}

	protected override Task Mount( MountContext context )
	{
		if ( !IsInstalled ) return Task.CompletedTask;

		var bundles = new BundleIndex( FileSystem, BundleDirectory, BuiltinResourceDirectory );
		var files = BundleGroups.SelectMany( bundles.FindGroup ).ToArray();
		bundles.UseSchemasFrom( files );
		var visuals = ReadVisualOverrides( bundles, files );
		var resources = new UnityResourceRegistry( Ident, bundles, visuals, ParticleGravity );
		foreach ( var file in files )
		{
			try
			{
				resources.Register( file );
			}
			catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
			{
				Log.Warning( $"Could not register Unity assets in '{file.Entry.Name}': {e.Message}" );
			}
		}
		foreach ( var file in files )
		{
			try
			{
				resources.RegisterContainerAssets( file );
			}
			catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
			{
				Log.Warning( $"Could not register Unity container assets in '{file.Entry.Name}': {e.Message}" );
			}
		}
		foreach ( var file in SpeedTreeGroups.SelectMany( bundles.FindGroup ).Except( files ) )
		{
			try
			{
				resources.RegisterSpeedTreeAssets( file );
			}
			catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
			{
				Log.Warning( $"Could not register Unity SpeedTree assets in '{file.Entry.Name}': {e.Message}" );
			}
		}
		var published = resources.Publish( context );
		var shared = resources.GetSharedAssets();
		var scenes = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		foreach ( var manifest in SceneGroups.SelectMany( bundles.FindSceneManifests ) )
		{
			try
			{
				foreach ( var (name, archive) in UnityResourceRegistry.FindScenes( manifest ) )
				{
					var file = bundles.FindSceneObjects( manifest, archive );
					if ( file is null )
					{
						Log.Warning( $"Unity scene '{name}' could not resolve archive '{archive}' in '{manifest.Entry.Name}'." );
						continue;
					}
					var path = $"maps/{name}.scene";
					if ( !scenes.Add( path ) ) continue;
					context.Add( ResourceType.Scene, path, new UnitySceneLoader( () => ReadSceneMarkers( bundles, file ), markers =>
					{
						var registry = new UnityResourceRegistry( Ident, bundles, visuals, ParticleGravity, path, shared );
						return registry.ReadScene( file, markers );
					} ) );
				}
			}
			catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
			{
				Log.Warning( $"Could not register Unity scene in '{manifest.Entry.Name}': {e.Message}" );
			}
		}
		IsMounted = published || scenes.Count > 0;
		return Task.CompletedTask;
	}
}

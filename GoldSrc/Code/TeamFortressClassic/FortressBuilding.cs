namespace GoldSrc.TeamFortressClassic;

internal enum BuildingKind
{
	Dispenser,
	Sentry,
	TeleporterEntrance,
	TeleporterExit
}

[Hide]
public abstract class FortressBuilding : Component, Component.IDamageable
{
	protected static GameMount Mount => Sandbox.Mounting.Directory.Get( "tfc" ) as TeamFortressClassicMount;

	[Sync( SyncFlags.FromHost )] public PlayerController Builder { get; private set; }
	[Sync( SyncFlags.FromHost )] public bool IsBuilt { get; private set; }
	[Sync( SyncFlags.FromHost )] public float Health { get; set; } = 150;
	public abstract float MaxHealth { get; }
	internal abstract BuildingKind Kind { get; }

	internal static T StartConstruction<T>( PlayerController builder, Vector3 position, Rotation rotation,
		string title, string modelName, float height, System.Action<T> configure = null ) where T : FortressBuilding, new()
	{
		if ( LoadModel( modelName ) is not { IsError: false } model ) return null;

		var go = new GameObject( false, title ) { WorldPosition = position, WorldRotation = rotation };
		go.Tags.Add( "removable" );
		go.AddComponent<ModelRenderer>().Model = model;
		var collider = go.AddComponent<BoxCollider>();
		collider.Center = Vector3.Up * (height * 0.5f);
		collider.Scale = new Vector3( 32, 32, height );
		collider.Static = true;
		var building = go.AddComponent<T>();
		building.Builder = builder;
		building.Health = building.MaxHealth;
		configure?.Invoke( building );
		go.NetworkSpawn( true, null );
		return building;
	}

	protected static Model LoadModel( string name ) => Mount?.FindMountPath( $"models/{name}.mdl" ) is { } path
		? Model.Load( $"mount://tfc/{path}.vmdl" )
		: null;

	internal void Finish( BaseInventoryComponent inventory )
	{
		if ( IsBuilt ) return;
		IsBuilt = true;
		OnBuilt( inventory );
	}

	internal abstract void OnBuilt( BaseInventoryComponent inventory );
	internal abstract void Service( BaseInventoryComponent inventory, BaseAmmoResource cells );

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return;
		if ( !Builder.IsValid() ) GameObject.Destroy();
		else if ( IsBuilt ) OnBuildingUpdate();
	}

	internal abstract void OnBuildingUpdate();

	public void OnDamage( in DamageInfo damage )
	{
		if ( !Networking.IsHost || !IsBuilt || Health <= 0 || !float.IsFinite( damage.Damage ) || damage.Damage <= 0 ) return;
		Health = System.MathF.Max( 0, Health - damage.Damage );
		if ( Health == 0 ) DestroyBuilding( damage.Attacker );
	}

	protected abstract void DestroyBuilding( GameObject attacker );
}

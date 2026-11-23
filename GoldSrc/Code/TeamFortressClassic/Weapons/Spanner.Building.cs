using GoldSrc.Maps;

namespace GoldSrc.TeamFortressClassic;

public sealed partial class Spanner
{
	TimeUntil _buildComplete;
	FortressBuilding _construction;
	PlayerController _slowedBuilder;
	int _buildCost;

	[Sync( SyncFlags.FromHost )] public bool IsBuilding { get; private set; }

	public override bool CanReload() => IsActive && !IsBuilding && Owner.IsValid() && PrimaryAmmoType is not null &&
		Ammo1 >= BuildCost( SelectedBuilding() );

	public override void Reload()
	{
		if ( Owner.IsValid() ) BeginBuilding( SelectedBuilding() );
	}

	BuildingKind SelectedBuilding()
	{
		var use = !string.IsNullOrEmpty( Owner.UseButton ) && Input.Down( Owner.UseButton );
		if ( use ) return Owner.IsDucking ? BuildingKind.TeleporterExit : BuildingKind.TeleporterEntrance;
		return Owner.IsDucking ? BuildingKind.Sentry : BuildingKind.Dispenser;
	}

	static int BuildCost( BuildingKind kind ) => kind switch
	{
		BuildingKind.Dispenser => 100,
		BuildingKind.Sentry => 130,
		_ => 125
	};

	[Rpc.Host( NetFlags.OwnerOnly )]
	void BeginBuilding( BuildingKind kind )
	{
		if ( kind is < BuildingKind.Dispenser or > BuildingKind.TeleporterExit ) return;
		var cost = BuildCost( kind );
		if ( !IsActive || IsBuilding || !CanBuild( Owner ) || PrimaryAmmoType is null || Ammo1 < cost ) return;
		if ( Scene.GetAllComponents<FortressBuilding>().Any( unit => unit.Builder == Owner && unit.Kind == kind ) ) return;

		var start = Owner.WorldPosition;
		var forward = Owner.EyeTransform.Forward.WithZ( 0 ).Normal;
		var end = start + forward * 64;
		var approach = Scene.Trace.Ray( start + Vector3.Up * 12, end + Vector3.Up * 12 )
			.IgnoreGameObjectHierarchy( Owner.GameObject ).WithoutTags( "trigger", "debris", "playercontroller" ).Run();
		if ( approach.Hit ) return;
		var floor = Scene.Trace.Ray( end + Vector3.Up * 16, end + Vector3.Down * 24 )
			.IgnoreGameObjectHierarchy( Owner.GameObject ).WithoutTags( "trigger", "debris", "playercontroller" ).Run();
		if ( !floor.Hit || floor.StartedSolid || floor.Normal.z < 0.7f ) return;

		var site = floor.EndPosition + Vector3.Up;
		var height = kind switch
		{
			BuildingKind.Sentry => FortressSentry.Height,
			BuildingKind.Dispenser => 24,
			_ => FortressTeleporter.PadHeight
		};
		var clearance = Scene.Trace.Box( new BBox( new Vector3( -16, -16, 0 ), new Vector3( 16, 16, height ) ),
			site, site + Vector3.Up ).IgnoreGameObjectHierarchy( Owner.GameObject ).WithoutTags( "trigger", "debris", "playercontroller" ).Run();
		if ( clearance.StartedSolid || clearance.Hit ) return;

		var rotation = Rotation.LookAt( forward );
		_construction = kind switch
		{
			BuildingKind.Sentry => FortressBuilding.StartConstruction<FortressSentry>( Owner, site, rotation, "TFC sentry", "base", height ),
			BuildingKind.Dispenser => FortressBuilding.StartConstruction<FortressDispenser>( Owner, site, rotation, "TFC dispenser", "dispenser", height ),
			_ => FortressTeleporter.Construct( Owner, site, rotation, kind == BuildingKind.TeleporterExit )
		};
		if ( !_construction.IsValid() ) return;

		_buildCost = cost;
		_buildComplete = kind switch
		{
			BuildingKind.Dispenser => 2,
			BuildingKind.Sentry => 5,
			_ => 4
		};
		IsBuilding = true;
		UpdateBuilderMovement();
		ContentMount?.NpcAudio.Emit( "weapons/building.wav", site );
	}

	static bool CanBuild( PlayerController player ) =>
		player.IsValid() && player.Enabled && player.IsOnGround && MapState.PlayerVitals( player ) is not { Health: <= 0 };

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( Networking.IsHost && IsBuilding )
		{
			if ( !_construction.IsValid() || !IsActive || !CanBuild( Owner ) || _construction.Builder != Owner || Ammo1 < _buildCost )
				StopBuilding();
			else if ( _buildComplete <= 0 )
			{
				if ( Inventory.TakeAmmo( PrimaryAmmoType, _buildCost ) == _buildCost )
					_construction.Finish( Inventory );
				StopBuilding();
			}
		}
		UpdateBuilderMovement();
	}

	protected override void OnDisabled()
	{
		StopBuilding();
		base.OnDisabled();
	}

	protected override void OnHolstered()
	{
		StopBuilding();
		base.OnHolstered();
	}

	void StopBuilding()
	{
		if ( Networking.IsHost )
		{
			if ( _construction.IsValid() && !_construction.IsBuilt ) _construction.GameObject.Destroy();
			_construction = null;
			IsBuilding = false;
		}
		ReleaseBuilder();
	}

	void UpdateBuilderMovement()
	{
		var builder = IsBuilding && IsActive && Owner is { IsProxy: false } ? Owner : null;
		if ( _slowedBuilder == builder ) return;
		ReleaseBuilder();
		if ( !builder.IsValid() ) return;
		_slowedBuilder = builder;
		FortressSpeed.SetBuilding( builder, true );
	}

	void ReleaseBuilder()
	{
		if ( _slowedBuilder.IsValid() ) FortressSpeed.SetBuilding( _slowedBuilder, false );
		_slowedBuilder = null;
	}
}

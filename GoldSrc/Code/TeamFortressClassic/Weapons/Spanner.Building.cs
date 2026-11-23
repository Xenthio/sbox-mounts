using GoldSrc.Maps;

namespace GoldSrc.TeamFortressClassic;

public sealed partial class Spanner
{
	const int BuildCost = 100;
	const float BuildTime = 2;
	TimeUntil _buildComplete;
	FortressDispenser _construction;
	PlayerController _slowedBuilder;

	[Sync( SyncFlags.FromHost )] public bool IsBuilding { get; private set; }

	public override bool CanReload() => IsActive && !IsBuilding && Owner.IsValid() && PrimaryAmmoType is not null && Ammo1 >= BuildCost;
	public override void Reload() => BeginBuilding();

	[Rpc.Host( NetFlags.OwnerOnly )]
	void BeginBuilding()
	{
		if ( !CanReload() || !CanBuild( Owner ) || Scene.GetAllComponents<FortressDispenser>().Any( unit => unit.Builder == Owner ) )
			return;

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
		var clearance = Scene.Trace.Box( new BBox( new Vector3( -16, -16, 0 ), new Vector3( 16, 16, 24 ) ),
			site, site + Vector3.Up ).IgnoreGameObjectHierarchy( Owner.GameObject ).WithoutTags( "trigger", "debris", "playercontroller" ).Run();
		if ( clearance.StartedSolid || clearance.Hit ) return;

		_construction = FortressDispenser.StartConstruction( Owner, site, Rotation.LookAt( forward ) );
		if ( !_construction.IsValid() ) return;

		_buildComplete = BuildTime;
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
			if ( !_construction.IsValid() || !IsActive || !CanBuild( Owner ) || _construction.Builder != Owner || Ammo1 < BuildCost )
				StopBuilding();
			else if ( _buildComplete <= 0 )
			{
				if ( Inventory.TakeAmmo( PrimaryAmmoType, BuildCost ) == BuildCost )
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
		FortressSpeed.SetBuildingDispenser( builder, true );
	}

	void ReleaseBuilder()
	{
		if ( _slowedBuilder.IsValid() ) FortressSpeed.SetBuildingDispenser( _slowedBuilder, false );
		_slowedBuilder = null;
	}
}

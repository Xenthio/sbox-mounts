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
		Ammo1 >= (Owner.IsDucking ? 130 : 100);
	public override void Reload()
	{
		if ( Owner.IsValid() ) BeginBuilding( Owner.IsDucking );
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void BeginBuilding( bool sentry )
	{
		var cost = sentry ? 130 : 100;
		if ( !IsActive || IsBuilding || !CanBuild( Owner ) || PrimaryAmmoType is null || Ammo1 < cost ) return;
		if ( Scene.GetAllComponents<FortressBuilding>().Any( unit => unit.Builder == Owner && (unit is FortressSentry) == sentry ) ) return;

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
		var height = sentry ? FortressSentry.Height : 24;
		var clearance = Scene.Trace.Box( new BBox( new Vector3( -16, -16, 0 ), new Vector3( 16, 16, height ) ),
			site, site + Vector3.Up ).IgnoreGameObjectHierarchy( Owner.GameObject ).WithoutTags( "trigger", "debris", "playercontroller" ).Run();
		if ( clearance.StartedSolid || clearance.Hit ) return;

		var rotation = Rotation.LookAt( forward );
		_construction = sentry
			? FortressBuilding.StartConstruction<FortressSentry>( Owner, site, rotation, "TFC sentry", "base", height )
			: FortressBuilding.StartConstruction<FortressDispenser>( Owner, site, rotation, "TFC dispenser", "dispenser", height );
		if ( !_construction.IsValid() ) return;

		_buildCost = cost;
		_buildComplete = sentry ? 5 : 2;
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

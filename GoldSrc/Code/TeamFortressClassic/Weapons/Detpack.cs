using GoldSrc.HalfLife;
using GoldSrc.Maps;

namespace GoldSrc.TeamFortressClassic;

[Title( "Detpack" ), Category( "Team Fortress Classic" )]
[WeaponPickup( "detpack", "tfc_detpacks", 1, PickupModel = "detpack", Bucket = 4, Mount = "tfc" )]
public sealed class Detpack : StudioWeapon
{
	const float PlantTime = 3;
	const float Clearance = 65;
	TimeUntil _plantComplete;
	PlayerController _planter, _slowedPlayer;
	int _fuse;

	[Sync( SyncFlags.FromHost )] public bool IsPlanting { get; private set; }

	internal override void Configure()
	{
		HoldType = WeaponHolds.Throwable;
		UsesClips = false;
		StartingAmmo = 1;
		PrimaryAutomatic = false;
	}

	protected override void CreateViewModel() { }
	protected override void CreateWorldModel() { }

	public override bool CanPrimaryAttack() => IsActive && !IsPlanting && base.CanPrimaryAttack();
	public override bool CanSecondaryAttack() => CanReload() && base.CanSecondaryAttack();
	public override bool CanReload() => IsActive && !IsPlanting && HasPrimaryAmmo();
	public override void PrimaryAttack() => BeginPlant( 5 );
	public override void SecondaryAttack() => BeginPlant( 20 );
	public override void Reload() => BeginPlant( 50 );

	[Rpc.Host( NetFlags.OwnerOnly )]
	void BeginPlant( int fuse )
	{
		if ( !IsActive || IsPlanting || !HasPrimaryAmmo() || fuse is not (5 or 20 or 50) ||
			!TryFindSite( Owner, out _ ) ) return;

		_planter = Owner;
		_fuse = fuse;
		_plantComplete = PlantTime;
		IsPlanting = true;
		UpdateMovement();
	}

	bool TryFindSite( PlayerController player, out Vector3 position )
	{
		position = default;
		if ( !player.IsValid() || !player.Enabled || !player.IsOnGround ||
			MapState.PlayerVitals( player ) is { Health: <= 0 } ) return false;

		var start = player.WorldPosition;
		if ( Scene.GetAllComponents<PlayerController>().Any( other => other != player && other.Enabled &&
			MapState.PlayerVitals( other ) is not { Health: <= 0 } && other.WorldPosition.DistanceSquared( start ) <= Clearance * Clearance ) ) return false;
		if ( Scene.GetAllComponents<FortressDetpack>().Any( pack => pack.Planter == player ||
			pack.WorldPosition.DistanceSquared( start ) <= Clearance * Clearance ) ) return false;
		if ( Scene.GetAllComponents<FortressDispenser>().Any( unit => unit.WorldPosition.DistanceSquared( start ) <= Clearance * Clearance ) )
			return false;

		var floor = Scene.Trace.Ray( start + Vector3.Up * 8, start + Vector3.Down * 12 )
			.IgnoreGameObjectHierarchy( player.GameObject ).WithoutTags( "trigger", "debris", "playercontroller" ).Run();
		if ( !floor.Hit || floor.StartedSolid || floor.Normal.z < 0.55f ) return false;
		position = floor.EndPosition + Vector3.Up * 4;
		return true;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( Networking.IsHost && IsPlanting )
		{
			if ( !IsActive || Owner != _planter || !_planter.IsValid() || !_planter.Enabled ||
				MapState.PlayerVitals( _planter ) is { Health: <= 0 } )
				IsPlanting = false;
			else if ( _plantComplete <= 0 )
			{
				IsPlanting = false;
				if ( TryFindSite( _planter, out var position ) && HasPrimaryAmmo() &&
					FortressDetpack.Plant( _planter, position, _fuse ) )
					TakePrimaryAmmo();
			}
		}
		UpdateMovement();
	}

	void UpdateMovement()
	{
		var player = IsPlanting && IsActive && Owner is { IsProxy: false } ? Owner : null;
		if ( _slowedPlayer == player ) return;
		ReleaseMovement();
		if ( !player.IsValid() ) return;
		FortressSpeed.SetPlantingDetpack( player, true );
		_slowedPlayer = player;
	}

	void ReleaseMovement()
	{
		if ( _slowedPlayer.IsValid() ) FortressSpeed.SetPlantingDetpack( _slowedPlayer, false );
		_slowedPlayer = null;
	}

	protected override void OnHolstered()
	{
		if ( Networking.IsHost ) IsPlanting = false;
		ReleaseMovement();
		base.OnHolstered();
	}

	protected override void OnDisabled()
	{
		if ( Networking.IsHost ) IsPlanting = false;
		ReleaseMovement();
		base.OnDisabled();
	}
}

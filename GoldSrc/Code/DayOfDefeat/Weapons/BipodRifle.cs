using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Hide]
public abstract class BipodRifle : DayOfDefeatFirearm, PlayerController.IEvents
{
	[Sync, Change( nameof( OnBipodChanged ) )]
	public bool BipodDeployed { get; private set; }

	TimeUntil _bipodBusy;
	float _deploymentYaw;
	MachineGunNest _supportNest;

	internal abstract string CarriedModel { get; }
	internal abstract string DeployedModel { get; }
	internal abstract float BipodTime { get; }
	internal abstract float CarriedReloadTime { get; }
	internal virtual float DeployedReloadTime => CarriedReloadTime;
	internal override float StandingSpread => 0.025f;
	internal override Vector2 PelletSpread => BipodDeployed ? Vector2.One * 0.02f : base.PelletSpread;
	internal override int BrassBodyGroup => 2;
	internal override string WorldModelPath => $"models/{(BipodDeployed ? DeployedModel : CarriedModel)}.mdl";

	string StancePrefix => BipodDeployed ? "down" : "up";
	internal override string DeploySequence => "up_draw";
	internal override string ShotSequence => $"{StancePrefix}_shoot";
	internal override string ReloadSequence => $"{StancePrefix}_reload";
	internal override (string Name, float? Delay) IdleAnimation => ($"{StancePrefix}_idle", 2.5f);

	internal override void Configure()
	{
		base.Configure();
		HoldType = WeaponHolds.Rifle;
		PrimaryAutomatic = true;
		ReloadTime = CarriedReloadTime;
		Ballistics = Ballistics with { Damage = 85 };
	}

	public override bool CanPrimaryAttack() => _bipodBusy <= 0 && base.CanPrimaryAttack();

	public override bool CanSecondaryAttack() =>
		IsActive && NextSecondaryFire <= 0 && _bipodBusy <= 0 && !IsReloading &&
		(BipodDeployed || (Owner is { } owner && HasStableFooting( owner ) && (owner.IsDucking || MachineGunNest.Find( owner ).IsValid())));

	public override bool CanReload() => _bipodBusy <= 0 && base.CanReload();

	protected override bool OnHolstering( BaseInventoryItem next ) =>
		!BipodDeployed && _bipodBusy <= 0 && base.OnHolstering( next );

	public override void SecondaryAttack()
	{
		if ( BipodDeployed )
		{
			SetBipod( false );
			return;
		}
		if ( Owner is not { } owner || !HasStableFooting( owner ) ) return;

		// Sandbox's crouch provides the portable low stance. Standing deployment uses the map's nest.
		var nest = owner.IsDucking ? null : MachineGunNest.Find( owner );
		if ( !owner.IsDucking && !nest.IsValid() ) return;
		_supportNest = nest;
		_deploymentYaw = owner.EyeAngles.yaw;
		SetBipod( true );
	}

	static bool HasStableFooting( PlayerController owner ) =>
		owner.IsOnGround && !owner.IsSwimming &&
		owner.Velocity.WithZ( 0 ).LengthSquared <= 400 && owner.WishVelocity.WithZ( 0 ).LengthSquared <= 1;

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( !IsProxy && IsActive && BipodDeployed && Owner is { } owner &&
			(!HasStableFooting( owner ) || !StillSupported( owner )) )
		{
			CancelReload();
			SetBipod( false );
		}
	}

	bool StillSupported( PlayerController owner ) =>
		_supportNest is null ? owner.IsDucking : _supportNest.IsValid() && _supportNest.Supports( owner );

	void PlayerController.IEvents.OnEyeAngles( ref Angles input )
	{
		if ( !IsActive || !BipodDeployed || Owner is not { } owner ) return;
		var current = owner.EyeAngles.yaw;
		var relative = Angles.NormalizeAngle( current + input.yaw - _deploymentYaw );
		var limited = _deploymentYaw + relative.Clamp( -45, 45 );
		input.yaw = Angles.NormalizeAngle( limited - current );
	}

	void SetBipod( bool deployed )
	{
		BipodDeployed = deployed;
		if ( !deployed ) _supportNest = null;
		_bipodBusy = BipodTime;
		SetNextFire( BipodTime );
		var view = ViewModel?.GetComponent<StudioWeaponView>();
		view?.Play( deployed ? "up_to_down" : "down_to_up" );
	}

	void OnBipodChanged( bool _, bool deployed )
	{
		ReloadTime = deployed ? DeployedReloadTime : CarriedReloadTime;
		if ( IsActive ) CreateWorldModel();
	}

	protected override void OnHolstered()
	{
		base.OnHolstered();
		BipodDeployed = false;
		_supportNest = null;
		_bipodBusy = 0;
	}
}

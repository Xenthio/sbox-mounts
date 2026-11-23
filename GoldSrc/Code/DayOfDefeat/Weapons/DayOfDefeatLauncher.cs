using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Hide]
public abstract class DayOfDefeatLauncher : ProjectileWeapon
{
	const float ShoulderTime = 0.8f;
	const float LowerTime = 1;
	const float ShoulderedSpeed = 50;

	TimeUntil _stanceChange;
	PlayerController _limitedPlayer;
	(float Walk, float Run, float Ducked) _normalSpeeds;

	[Sync( SyncFlags.FromHost )] public bool Shouldered { get; private set; }

	internal abstract string CarriedModel { get; }
	internal abstract string ShellModel { get; }
	internal abstract float CarriedReloadTime { get; }
	internal abstract float FireDelay { get; }
	internal virtual float ShoulderedReloadTime => CarriedReloadTime;
	internal virtual bool HasEmptySequences => false;
	internal override int FreshPickupRounds => 5;
	internal override string WorldModelPath => $"models/{CarriedModel}.mdl";
	internal override string DeploySequence => WithEmptyVariant( "draw" );
	internal override string ShotSequence => "launch";
	internal override float? IdleAfterShot => 1;
	internal override string ReloadSequence => Shouldered ? "reload_aimed" : "reload_idle";
	internal override float IdleAfterReloadStart => ReloadTime;
	internal override (string Name, float? Delay) IdleAnimation => (WithEmptyVariant( Shouldered ? "aimed" : "idle" ), null);
	public override Ray AimRay => Owner is { } owner ? owner.EyeTransform.ForwardRay : base.AimRay;

	string WithEmptyVariant( string sequence ) => HasEmptySequences && Clip1 == 0 ? $"{sequence}_empty" : sequence;

	internal override void Configure()
	{
		HoldType = WeaponHolds.Rifle;
		ClipMaxSize = 1;
		DeployTime = 1;
		PrimaryDelay = FireDelay;
		SecondaryDelay = ShoulderTime;
		ReloadTime = CarriedReloadTime;
		AutoReload = false;
	}

	bool Immersed( PlayerController owner ) =>
		WaterBody.Find( Scene, owner.WorldPosition + (owner.EyePosition - owner.WorldPosition) * 0.5f ).IsValid();

	bool CanLaunch() => IsActive && Shouldered && !IsReloading && _stanceChange <= 0 && Owner is { } owner && !Immersed( owner );

	public override bool CanPrimaryAttack() => CanLaunch() && base.CanPrimaryAttack();

	public override bool CanSecondaryAttack() =>
		IsActive && _stanceChange <= 0 && !IsReloading && NextSecondaryFire <= 0 &&
		Owner is { } owner && (Shouldered || !Immersed( owner ));

	public override void SecondaryAttack() => ToggleShoulder();

	[Rpc.Host( NetFlags.OwnerOnly )]
	void ToggleShoulder()
	{
		if ( !IsActive || _stanceChange > 0 || IsReloading || Owner is not { } owner || (!Shouldered && Immersed( owner )) ) return;

		Shouldered = !Shouldered;
		_stanceChange = Shouldered ? ShoulderTime : LowerTime;
		ShowShoulder( WithEmptyVariant( Shouldered ? "down_to_up" : "up_to_down" ), _stanceChange );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowShoulder( string sequence, float duration )
	{
		_stanceChange = duration;
		SetNextFire( duration );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( sequence );
	}

	internal override bool Release( Ray ray )
	{
		if ( !CanLaunch() || ContentMount?.FindFile( $"models/{ShellModel}.mdl" ) is not { } path ) return false;
		var muzzle = TraceLineOfFire( ray, 32, 0 ).Run();
		// Leave a blocked muzzle on the near side so the projectile contacts the obstruction.
		var origin = muzzle.Hit ? muzzle.EndPosition - ray.Forward : muzzle.EndPosition;
		DayOfDefeatShell.Launch( Owner.GameObject, $"mount://{Definition.Mount}/{path}.vmdl", origin, ray.Forward );
		return true;
	}

	public override bool CanReload() => _stanceChange <= 0 && base.CanReload();

	protected override void OnReloadStarted()
	{
		ReloadTime = Shouldered ? ShoulderedReloadTime : CarriedReloadTime;
		base.OnReloadStarted();
	}

	protected override bool OnHolstering( BaseInventoryItem next ) =>
		(!Shouldered || IsReloading) && base.OnHolstering( next );

	protected override void OnHolstered()
	{
		base.OnHolstered();
		if ( Networking.IsHost ) Shouldered = false;
		RestoreMovement();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( IsProxy || !IsActive || !Shouldered || IsReloading || Owner is not { } owner )
		{
			RestoreMovement();
			return;
		}
		if ( _limitedPlayer == owner ) return;
		RestoreMovement();
		_limitedPlayer = owner;
		_normalSpeeds = (owner.WalkSpeed, owner.RunSpeed, owner.DuckedSpeed);
		owner.WalkSpeed = Limit( owner.WalkSpeed );
		owner.RunSpeed = Limit( owner.RunSpeed );
		owner.DuckedSpeed = Limit( owner.DuckedSpeed );
	}

	protected override void OnDisabled()
	{
		RestoreMovement();
		base.OnDisabled();
	}

	static float Limit( float speed ) => System.MathF.Min( speed, ShoulderedSpeed );

	void RestoreMovement()
	{
		if ( _limitedPlayer.IsValid() )
		{
			if ( _limitedPlayer.WalkSpeed == Limit( _normalSpeeds.Walk ) ) _limitedPlayer.WalkSpeed = _normalSpeeds.Walk;
			if ( _limitedPlayer.RunSpeed == Limit( _normalSpeeds.Run ) ) _limitedPlayer.RunSpeed = _normalSpeeds.Run;
			if ( _limitedPlayer.DuckedSpeed == Limit( _normalSpeeds.Ducked ) ) _limitedPlayer.DuckedSpeed = _normalSpeeds.Ducked;
		}
		_limitedPlayer = null;
	}
}

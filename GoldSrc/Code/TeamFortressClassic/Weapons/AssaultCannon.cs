using GoldSrc.HalfLife;

namespace GoldSrc.TeamFortressClassic;

[Title( "Assault Cannon" ), Category( "Team Fortress Classic" )]
[WeaponPickup( "tfac", "buckshot", 200, EntityName = "tf_weapon_ac", PickupModel = "p_mini", Bucket = 4, Weight = 15, Mount = "tfc" )]
public sealed class AssaultCannon : BulletWeapon
{
	enum BarrelState { Idle, SpinningUp, Firing, SpinningEmpty, SpinningDown }
	const float FireInterval = 0.1f;
	const float SpinUpTime = 0.5f;
	const float SpinDownTime = 2;
	const float FiringSpeedLimit = 80;
	[Sync, Change( nameof( OnBarrelsChanged ) )] BarrelState Barrels { get; set; }
	TimeUntil _phaseEnds;
	TimeUntil _lastShotEnds;
	PlayerController _limitedPlayer;
	SoundHandle _barrelSound;
	int _tracers;
	bool BarrelsEngaged => Barrels is BarrelState.SpinningUp or BarrelState.Firing or BarrelState.SpinningEmpty;

	internal override string WorldModelPath => "models/p_mini.mdl";
	internal override string ShotSequence => "fire";
	internal override float? IdleAfterShot => float.PositiveInfinity;
	internal override bool SwitchWhenSpent => false;
	internal override Vector2 PelletSpread => Vector2.One * 0.1f;
	internal override int PlayerMuzzleFlash => 51;
	internal override (string Name, float? Delay) IdleAnimation => (Game.Random.Int( 0, 1 ) == 0 ? "idle" : "idle2", 12.5f);

	internal override void Configure()
	{
		HoldType = WeaponHolds.Rifle;
		UsesClips = false;
		StartingAmmo = 25;
		PrimaryDelay = FireInterval;
		AttackDamageTags = ["bullet"];
		Ballistics = new() { Pellets = 5, Damage = 8, Range = 8192, Radius = 0 };
	}

	public override Ray AimRay => Owner.IsValid() ? OffsetLaunchRay( Vector3.Left * 2 + Vector3.Down * 4 ) : base.AimRay;
	public override bool CanPrimaryAttack() => Barrels == BarrelState.Firing && base.CanPrimaryAttack();
	public override bool CanSecondaryAttack() => false;
	public override void DryFire() { }

	protected override void Think() => UpdateBarrels( WantsPrimaryAttack() );

	void UpdateBarrels( bool triggerHeld )
	{
		if ( !IsActive || !Owner.IsValid() ) return;
		if ( !triggerHeld )
		{
			if ( BarrelsEngaged && _lastShotEnds <= 0 )
			{
				_phaseEnds = SpinDownTime;
				Barrels = BarrelState.SpinningDown;
			}
			else if ( Barrels == BarrelState.SpinningDown && _phaseEnds <= 0 )
				Barrels = BarrelState.Idle;
			return;
		}

		switch ( Barrels )
		{
			case BarrelState.Idle or BarrelState.SpinningDown when Owner.IsOnGround && NextPrimaryFire <= 0:
				_phaseEnds = SpinUpTime;
				_lastShotEnds = SpinUpTime + FireInterval;
				Barrels = BarrelState.SpinningUp;
				break;
			case BarrelState.SpinningUp when _phaseEnds <= 0:
				SetNextPrimaryFire( FireInterval );
				Barrels = HasPrimaryAmmo() ? BarrelState.Firing : BarrelState.SpinningEmpty;
				break;
			case BarrelState.Firing when !HasPrimaryAmmo():
				Barrels = BarrelState.SpinningEmpty;
				break;
			case BarrelState.SpinningEmpty when HasPrimaryAmmo():
				_phaseEnds = FireInterval;
				Barrels = BarrelState.SpinningUp;
				break;
		}
	}

	public override void PrimaryAttack()
	{
		if ( !IsActive || Barrels != BarrelState.Firing || !HasPrimaryAmmo() ) return;
		_lastShotEnds = FireInterval;
		base.PrimaryAttack();
	}

	internal override void DrawShotTrail( Vector3 start, Vector3 end )
	{
		if ( (_tracers++ & 7) == 0 ) base.DrawShotTrail( start, end );
	}

	protected override void OnShootEffects( ShotEffect shot )
	{
		base.OnShootEffects( shot );
		if ( !shot.NoEvents && (Ammo1 & 1) == 0 )
			EjectShells( 1, false, "models/shell.mdl", new Vector3( 15, -6, -25 ) );
	}

	void OnBarrelsChanged( BarrelState previous, BarrelState current )
	{
		if ( !IsActive ) return;
		if ( ViewModel?.GetComponent<StudioWeaponView>() is { } view )
		{
			switch ( current )
			{
				case BarrelState.SpinningUp when previous != BarrelState.SpinningEmpty:
					view.Play( "spinup", holdLastFrame: true );
					break;
				case BarrelState.Firing or BarrelState.SpinningEmpty:
					view.Play( "fire", idleAfter: float.PositiveInfinity );
					break;
				case BarrelState.SpinningDown:
					view.Play( "spindown", idleAfter: SpinDownTime );
					break;
			}
		}

		if ( current == BarrelState.Idle || current == BarrelState.SpinningUp && previous == BarrelState.SpinningEmpty ) return;
		_barrelSound?.Stop();
		var sample = current switch
		{
			BarrelState.SpinningUp => "weapons/asscan1.wav",
			BarrelState.Firing => "weapons/asscan2.wav",
			BarrelState.SpinningDown => "weapons/asscan3.wav",
			BarrelState.SpinningEmpty => "weapons/asscan4.wav",
			_ => null
		};
		_barrelSound = sample is null ? null : PlaySample( sample, 0.98f, 1.25f );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( _barrelSound.IsValid() ) _barrelSound.Position = WorldPosition;
		if ( !Networking.IsHost && IsProxy ) return;
		var player = IsActive && BarrelsEngaged ? Owner : null;
		if ( _limitedPlayer == player ) return;
		RestoreMovement();
		if ( !player.IsValid() ) return;
		FortressSpeed.SetAimLimit( player, FiringSpeedLimit );
		_limitedPlayer = player;
	}

	void RestoreMovement()
	{
		if ( _limitedPlayer.IsValid() ) FortressSpeed.SetAimLimit( _limitedPlayer, null );
		_limitedPlayer = null;
	}

	void ResetBarrels()
	{
		if ( !IsProxy ) Barrels = BarrelState.Idle;
		_barrelSound?.Stop();
		_barrelSound = null;
		RestoreMovement();
	}

	protected override void OnHolstered()
	{
		ResetBarrels();
		base.OnHolstered();
	}

	protected override void OnDisabled()
	{
		ResetBarrels();
		base.OnDisabled();
	}
}

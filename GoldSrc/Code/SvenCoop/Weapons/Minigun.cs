using GoldSrc.HalfLife;

namespace GoldSrc.SvenCoop;

[Title( "XM214 Microgun" ), Category( "Sven Co-op" )]
[WeaponPickup( "minigun", "556", 600, Bucket = 9, Weight = 25, Mount = "svencoop" )]
public sealed partial class Minigun : BulletWeapon, Component.IPressable
{
	enum BarrelState { Idle, Starting, Ready, Firing, Stopping }
	const float SpinDownTime = 2;
	const float SpinRestartDelay = 0.5f;

	[Sync, Change( nameof( OnBarrelsChanged ) )]
	private BarrelState Barrels { get; set; }
	TimeUntil _spinTransition;
	TimeUntil _deploying;
	SoundHandle _spinSound, _fireSound;

	internal override string WorldModelPath => Barrels == BarrelState.Idle ? "models/p_minigunidle.mdl" : "models/p_minigunspin.mdl";
	internal override Vector2 PelletSpread => Vector2.One * (Owner?.IsDucking == true ? 0.04362f : 0.08716f);
	internal override int PlayerMuzzleFlash => 21;
	internal override string ShotSound => "minigun/hw_shoot1.wav";
	// The firing cycle stays animated between volleys.
	internal override string ShotSequence => null;
	internal override bool SwitchWhenSpent => false;
	internal override (string Name, float? Delay) IdleAnimation => Barrels switch
	{
		BarrelState.Ready => ("spinidle", float.PositiveInfinity),
		BarrelState.Firing => ("spinfire", float.PositiveInfinity),
		_ => ("stillidle", float.PositiveInfinity)
	};

	internal override void Configure()
	{
		HoldType = WeaponHolds.Rifle;
		UsesClips = false;
		StartingAmmo = 400;
		PrimaryAutomatic = SecondaryAutomatic = true;
		PrimaryDelay = SecondaryDelay = 0.06f;
		DeployTime = 2;
		AttackDamageTags = ["bullet"];
		Ballistics = new() { Pellets = 2, Damage = 15, Range = 4096, Radius = 0 };
	}

	bool CanSpin() => IsActive && _deploying <= 0 && Owner is { IsClimbing: false };
	public override bool CanPrimaryAttack() => CanSpin() && (Barrels is BarrelState.Ready or BarrelState.Firing) && base.CanPrimaryAttack();
	public override bool CanSecondaryAttack() => CanSpin() && base.CanSecondaryAttack();
	public override bool CanReload() => false;
	protected override bool OnHolstering( BaseInventoryItem next ) => false;

	protected override void OnEquipped()
	{
		_deploying = DeployTime;
		base.OnEquipped();
	}

	protected override void Think()
	{
		var wantsFire = WantsPrimaryAttack() && HasPrimaryAmmo();
		if ( CanSpin() && (wantsFire || WantsSecondaryAttack()) )
			AdvanceSpin( wantsFire );
		else
			StopSpin();
	}

	void AdvanceSpin( bool firing )
	{
		if ( Barrels == BarrelState.Stopping && _spinTransition > SpinDownTime - SpinRestartDelay ) return;
		if ( Barrels is BarrelState.Idle or BarrelState.Stopping )
		{
			_spinTransition = firing ? 1 : 0.9f;
			Barrels = BarrelState.Starting;
		}
		else if ( Barrels == BarrelState.Starting && _spinTransition <= 0 || Barrels == BarrelState.Firing && !firing )
			Barrels = BarrelState.Ready;
	}

	void StopSpin()
	{
		if ( Barrels is BarrelState.Starting or BarrelState.Ready or BarrelState.Firing )
		{
			_spinTransition = SpinDownTime;
			Barrels = BarrelState.Stopping;
		}
		else if ( Barrels == BarrelState.Stopping && _spinTransition <= 0 )
			Barrels = BarrelState.Idle;
	}

	public override void SecondaryAttack()
	{
		if ( CanSpin() ) AdvanceSpin( WantsPrimaryAttack() );
	}

	public override void PrimaryAttack()
	{
		if ( !CanSpin() || Barrels is not (BarrelState.Ready or BarrelState.Firing) || !HasPrimaryAmmo() ) return;
		Barrels = BarrelState.Firing;
		base.PrimaryAttack();
	}

	protected override void OnShootEffects( ShotEffect shot )
	{
		base.OnShootEffects( shot );
		if ( !shot.NoEvents ) EjectShells( 1, false, "models/saw_shell.mdl" );
	}

	internal override void PlayShotSound()
	{
		_fireSound?.Stop();
		_fireSound = PlaySample( ShotSound );
	}

	internal override void SetupHeldModel( SkinnedModelRenderer renderer )
	{
		if ( Barrels == BarrelState.Idle ) return;
		renderer.UseAnimGraph = false;
		renderer.Sequence.Name = "idle";
		renderer.Sequence.Looping = true;
	}

	void OnBarrelsChanged( BarrelState previous, BarrelState current )
	{
		if ( !IsActive ) return;
		if ( previous == BarrelState.Firing && current != BarrelState.Firing ) StopFireSound();
		if ( (previous == BarrelState.Idle) != (current == BarrelState.Idle) ) CreateWorldModel();
		if ( ViewModel?.GetComponent<StudioWeaponView>() is { } view )
		{
			var looping = current is BarrelState.Idle or BarrelState.Ready or BarrelState.Firing;
			var sequence = current switch
			{
				BarrelState.Starting => "spinup",
				BarrelState.Ready => "spinidle",
				BarrelState.Firing => "spinfire",
				BarrelState.Stopping => previous == BarrelState.Firing ? "spindown" : "spinidledown",
				_ => "stillidle"
			};
			view.Play( sequence, idleAfter: looping ? float.PositiveInfinity : null );
		}
		UpdateSpinSound( previous, current );
	}

	void UpdateSpinSound( BarrelState previous, BarrelState current )
	{
		if ( previous is BarrelState.Ready or BarrelState.Firing && current is BarrelState.Ready or BarrelState.Firing ) return;
		_spinSound?.Stop();
		_spinSound = current switch
		{
			BarrelState.Starting => PlaySample( "hassault/hw_spinup.wav" ),
			BarrelState.Ready or BarrelState.Firing => PlaySample( "hassault/hw_spin.wav" ),
			BarrelState.Stopping => PlaySample( "hassault/hw_spindown.wav" ),
			_ => null
		};
	}

	protected override void OnHolstered()
	{
		base.OnHolstered();
		if ( !IsProxy ) Barrels = BarrelState.Idle;
		RestoreMovement();
		_spinSound?.Stop();
		_spinSound = null;
		StopFireSound();
	}

	protected override void OnDisabled()
	{
		RestoreMovement();
		_spinSound?.Stop();
		_spinSound = null;
		StopFireSound();
		base.OnDisabled();
	}

	void StopFireSound()
	{
		_fireSound?.Stop();
		_fireSound = null;
	}
}

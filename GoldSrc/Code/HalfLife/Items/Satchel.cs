namespace GoldSrc.HalfLife;

[Title( "Satchel Charge" ), Category( "Half-Life" )]
[WeaponPickup( "satchel", "satchels", 5, AlternateModel = "satchel_radio" )]
public sealed class Satchel : ClassicSatchel;

[Hide]
public abstract class ClassicSatchel : ProjectileWeapon
{
	const byte Charge = 0, Radio = 1, Detonating = 2;
	[Sync( SyncFlags.FromHost ), Change( nameof( OnStateChanged ) )] byte State { get; set; }
	TimeUntil _radioReady, _throwReady, _redraw;

	internal virtual bool DetonateWithPrimary => false;
	internal virtual GameMount ChargeMount => null;

	internal override string HeldModelName => State == Charge ? Definition.Model : Definition.AlternateModel;
	internal override string ShotSequence => "draw";
	internal override bool CanIdle => State != Detonating;
	internal override void PlayIdle( StudioWeaponView view ) => view.Play( "fidget1", idleAfter: Game.Random.Float( 10, 15 ) );
	public override bool ShouldAvoid => State == Charge && base.ShouldAvoid;

	void OnStateChanged( byte before, byte after )
	{
		if ( after == Detonating )
		{
			ViewModel?.GetComponent<StudioWeaponView>()?.Play( "fire" );
			return;
		}

		if ( ViewModel.IsValid() )
		{
			DestroyViewModel();
			CreateViewModel();
		}
		if ( WorldModel.IsValid() ) CreateWorldModel();
	}

	internal override void Configure()
	{
		HoldType = WeaponHolds.Throwable;
		DeployTime = PrimaryDelay = 1;
		SecondaryDelay = 0.5f;
		UsesClips = false;
		StartingAmmo = 1;
	}

	public override bool CanPrimaryAttack() => State != Detonating &&
		(DetonateWithPrimary && State == Radio ? NextPrimaryFire <= 0 : base.CanPrimaryAttack());
	public override bool CanSecondaryAttack() => base.CanSecondaryAttack() &&
		(DetonateWithPrimary ? State != Detonating && HasPrimaryAmmo() : State == Radio);

	public override void PrimaryAttack()
	{
		if ( DetonateWithPrimary && State == Radio ) DetonateCharges();
		else base.PrimaryAttack();
	}

	internal override bool Release( Ray ray )
	{
		if ( State == Detonating || _redraw > 0 || _throwReady > 0 ) return false;
		if ( ChargeMount is { } mount )
		{
			if ( !PlacedCharge.SpawnSatchel( Owner, ray, mount, damage: 150 ) ) return false;
		}
		else PlacedCharge.Throw( Owner, ray );
		State = Radio;
		_radioReady = DetonateWithPrimary ? PrimaryDelay : SecondaryDelay;
		if ( DetonateWithPrimary )
		{
			_throwReady = SecondaryDelay;
			SetRadioCooldowns( PrimaryDelay );
		}
		return true;
	}

	public override void SecondaryAttack()
	{
		if ( DetonateWithPrimary ) ThrowAnotherCharge( AimRay );
		else DetonateCharges();
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void ThrowAnotherCharge( Ray ray ) => TryLaunch( ray, Release, secondary: true );

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void SetRadioCooldowns( float primaryDelay )
	{
		SetNextPrimaryFire( primaryDelay );
		SetNextSecondaryFire( SecondaryDelay );
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void DetonateCharges()
	{
		if ( !IsActive || !Owner.IsValid() || State != Radio || _radioReady > 0 ) return;
		State = Detonating;
		_redraw = 0.5f;
		if ( DetonateWithPrimary ) SetRadioCooldowns( 0.5f );
		foreach ( var charge in Scene.GetAllComponents<PlacedCharge>().Where( x => !x.IsMine && x.Thrower == Owner.GameObject &&
			x.EffectsMount == ChargeMount && x.WorldPosition.DistanceSquared( Owner.WorldPosition ) <= 4096 * 4096 ).ToArray() )
			charge.Detonate();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( !Networking.IsHost || State != Detonating || _redraw > 0 ) return;
		State = Charge;
		_redraw = 0.5f;
		if ( DetonateWithPrimary ) SetRadioCooldowns( 0.5f );
		if ( IsHeld && !HasPrimaryAmmo() ) Inventory.Remove( this );
	}
}

namespace GoldSrc.HalfLife;

[Title( "Hivehand" ), Category( "Half-Life" )]
[WeaponPickup( "hgun", "hornets", 8, ClassName = "hornetgun", Bucket = 3, Weight = 15 )]
public sealed class Hivehand : ClassicHivehand;

[Hide]
public abstract class ClassicHivehand : ProjectileWeapon
{
	internal override string DeploySequence => "up";
	internal override string ShotSequence => "Shoot";
	internal override float? IdleAfterShot => Game.Random.Float( 10, 15 );
	internal override string ShotSound => $"agrunt/ag_fire{Game.Random.Int( 1, 3 )}.wav";
	internal override (string Name, float? Delay) IdleAnimation => Game.Random.Float( 0, 1 ) switch
	{
		<= 0.75f => ("idle1", 60f / 16),
		<= 0.875f => ("fidgetSway", 40f / 16),
		_ => ("fidgetShake", 35f / 16)
	};
	internal virtual float RechargeInterval => Networking.MaxPlayers > 1 ? 0.3f : 0.5f;
	internal virtual float RechargeDelay => RechargeInterval;
	internal virtual float EmptyPrimaryDelay => RechargeInterval;
	internal virtual float EmptySecondaryDelay => 0.5f;
	static float HornetDamage => Networking.MaxPlayers > 1 ? 10 : 7;
	int _phase;
	TimeUntil _regenerate;
	internal override Vector3 WorldMuzzleOffset( bool hd ) => hd ? new( 19.1f, 0.7f, -2.2f ) : new( 19.3f, 0.9f, -0.6f );
	public override Ray AimRay => Owner.IsValid() ? OffsetLaunchRay( new Vector3( 16, -8, -12 ) ) : base.AimRay;

	internal override void Configure()
	{
		HoldType = WeaponHolds.Rifle;
		PrimaryDelay = 0.25f;
		SecondaryDelay = 0.1f;
		SecondaryAutomatic = true;
		UsesClips = false;
		StartingAmmo = 8;
	}

	public override bool HasSecondaryAmmo() => HasPrimaryAmmo();
	public override void PrimaryAttack()
	{
		SetNextPrimaryFire( PrimaryDelay + (Ammo1 == 1 ? EmptyPrimaryDelay : 0) );
		LaunchHornet( AimRay, false );
	}

	public override void SecondaryAttack()
	{
		SetNextFire( SecondaryDelay + (Ammo1 == 1 ? EmptySecondaryDelay : 0) );
		LaunchHornet( AimRay, true );
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void LaunchHornet( Ray ray, bool dart )
	{
		var emptyDelay = dart ? EmptySecondaryDelay : EmptyPrimaryDelay;
		TryLaunch( ray, dart ? ReleaseDart : Release, dart, Ammo1 == 1 ? emptyDelay : 0 );
	}

	internal override bool Release( Ray ray )
	{
		if ( !ReleaseTrackingHornet( ray ) ) return false;
		_regenerate = RechargeDelay;
		return true;
	}

	internal virtual bool ReleaseTrackingHornet( Ray ray ) => SpawnHornet<Hornet>( ray.Position, ray.Forward, 300 );

	bool ReleaseDart( Ray ray )
	{
		var up = _phase is 7 or 0 or 1 ? 8 : _phase is 3 or 4 or 5 ? -8 : 0;
		var right = _phase is 1 or 2 or 3 ? 8 : _phase is 5 or 6 or 7 ? -8 : 0;
		var basis = Rotation.LookAt( ray.Forward );
		var origin = ray.Position + basis.Up * up + basis.Right * right;
		if ( !SpawnHornet<HornetDart>( origin, ray.Forward, 1200 ) ) return false;
		_phase = (_phase + 1) % 8;
		_regenerate = Ammo1 == 1 && EmptySecondaryDelay > 0 ? EmptySecondaryDelay : RechargeDelay;
		return true;
	}

	protected bool SpawnHornet<T>( Vector3 origin, Vector3 direction, float speed, System.Action<T> configure = null ) where T : Hornet, new()
	{
		if ( ContentMount is not { } mount || mount.FindMountPath( "models/hornet.mdl" ) is not { } path ) return false;
		NpcProjectile.Fire<T>( Owner.GameObject, $"mount://{mount.Ident}/{path}.vmdl", HornetDamage, null, origin, direction, speed, configure );
		return true;
	}

	internal override void PlayLaunchSound()
	{
		base.PlayLaunchSound();
		if ( !IsProxy && Owner is { ThirdPerson: false } )
			MeleeViewPunch.Apply( Scene.Camera, new Angles( Game.Random.Int( 0, 2 ), 0, 0 ) );
	}

	protected override void OnHolstered()
	{
		if ( Networking.IsHost && Inventory.IsValid() && Ammo1 == 0 ) Inventory.GiveAmmo( PrimaryAmmoType, 1 );
		base.OnHolstered();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( !Networking.IsHost || !Inventory.IsValid() || Ammo1 >= MaxReserveAmmo || _regenerate > 0 ) return;
		var rounds = 1 + (int)(-(float)_regenerate / RechargeInterval);
		_regenerate = (float)_regenerate + rounds * RechargeInterval;
		Inventory.GiveAmmo( PrimaryAmmoType, rounds );
	}
}

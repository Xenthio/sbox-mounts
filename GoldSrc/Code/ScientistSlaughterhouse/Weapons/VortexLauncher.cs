using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

[Title( "Vortex Launcher" ), Category( "Scientist Slaughterhouse" )]
[WeaponPickup( "vortexlauncher", "vortex_m", 50, AlternateModel = "rpg", Bucket = 6, Weight = 30, Mount = "ssh" )]
public sealed class VortexLauncher : StudioWeapon
{
	[Sync( SyncFlags.FromHost )] VortexRocket RemoteRocket { get; set; }
	TimeUntil _primaryReady, _secondaryReady;

	internal override string HeldModelName => Definition.AlternateModel;
	internal override string WorldModelPath => Definition.ModelPath( "p_vortexlauncher" );
	internal override string DeploySequence => Clip1 > 0 ? "draw1" : "draw2";
	internal override string ShotSequence => "fire";
	internal override int FreshPickupRounds => 5;
	internal override float IdleAfterReloadStart => Game.Random.Float( 10, 15 );

	internal override void Configure()
	{
		HoldType = WeaponHolds.Shoulder;
		ClipMaxSize = 1;
		PrimaryDelay = 1.5f;
		SecondaryDelay = 0.5f;
	}

	internal override bool CanIdle => Ammo1 > 0;
	internal override (string Name, float? Delay) IdleAnimation => (Clip1 > 0, Game.Random.Float( 0, 1 ) < 0.75f) switch
	{
		(true, true) => ("idle", 6),
		(true, false) => ("fidget", 3),
		(false, true) => ("idle2", 6),
		_ => ("fidget2", 3)
	};

	public override bool HasSecondaryAmmo() => RemoteRocket.IsValid() || HasPrimaryAmmo();
	public override void PrimaryAttack() => FireRocket( LaunchRay(), false );
	public override void SecondaryAttack() => FireRocket( LaunchRay(), true );

	Ray LaunchRay() => Owner.IsValid() ? OffsetLaunchRay( new Vector3( 16, -8, -8 ) ) : AimRay;

	[Rpc.Host( NetFlags.OwnerOnly )]
	void FireRocket( Ray aim, bool secondary )
	{
		if ( !IsActive || !Owner.IsValid() || (secondary ? _secondaryReady : _primaryReady) > 0 ) return;
		if ( secondary && RemoteRocket.IsValid() )
		{
			RemoteRocket.DeployField( RemoteRocket.WorldPosition );
			RemoteRocket = null;
			_secondaryReady = 1.5f;
			ShowRemoteDetonation();
			return;
		}
		if ( !aim.Position.IsFinite || !aim.Forward.IsFinite || aim.Position.Distance( Owner.EyePosition ) > 96 || !TakePrimaryAmmo() ) return;
		var model = "mount://ssh/valve/models/rpgrocket.mdl.vmdl";
		if ( secondary )
			RemoteRocket = NpcProjectile.Fire<BlackHoleRocket>( Owner.GameObject, model, 0, null, aim.Position, aim.Forward, 750 );
		else
			NpcProjectile.Fire<SeekingVortexRocket>( Owner.GameObject, model, 250, null, aim.Position, aim.Forward, 200 );

		_primaryReady = 1.5f;
		_secondaryReady = secondary ? 0.5f : 1.5f;
		ShowFire( secondary );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowFire( bool secondary )
	{
		SetNextPrimaryFire( 1.5f );
		SetNextSecondaryFire( secondary ? 0.5f : 1.5f );
		PlaySample( "weapons/rocketfire1.wav", 0.9f );
		PlaySample( "weapons/glauncher.wav", 0.7f );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( ShotSequence, idleAfter: 1.5f );
		PostWeaponEvent( WeaponEvents.Attack );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowRemoteDetonation() => SetNextSecondaryFire( 1.5f );
}

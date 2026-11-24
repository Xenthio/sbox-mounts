using GoldSrc.HalfLife;
using GoldSrc.Maps;

namespace GoldSrc.ScientistSlaughterhouse;

internal enum RocketGuidance { Unguided, LaserGuided, Heatseeking, RemoteControlled }
internal enum RocketWarhead { HighExplosive, Incendiary, PlasmaBomb, Emp }

[Title( "Rocket Launcher" ), Category( "Scientist Slaughterhouse" )]
[WeaponPickup( "rpg", "rockets", 50, Bucket = 3, Weight = 20, SkipAutoSelect = true, Mount = "ssh" )]
public sealed class RocketLauncher : GuidedRocketLauncher
{
	[Sync( SyncFlags.FromHost )] internal RocketGuidance Guidance { get; set; } = RocketGuidance.LaserGuided;
	[Sync( SyncFlags.FromHost )] internal RocketWarhead Warhead { get; set; }
	[Sync( SyncFlags.FromHost )] internal Rocket RemoteRocket { get; set; }
	TimeUntil _nextGuidanceChange;
	TimeUntil _nextWarheadChange;

	internal override int FreshPickupRounds => 5;
	internal override bool HasDesignator => Guidance == RocketGuidance.LaserGuided;
	public override Ray AimRay => Owner.IsValid() ? OffsetLaunchRay( new Vector3( 16, -8, -8 ) ) : base.AimRay;

	internal override void Configure()
	{
		base.Configure();
		ReloadTime = 2;
	}

	internal override PlayerRocket LaunchRocket( Ray ray )
	{
		if ( ContentMount is not { } mount || mount.FindMountPath( "models/rpgrocket.mdl" ) is not { } path ) return null;
		var rocket = NpcProjectile.Fire<Rocket>( Owner.GameObject, $"mount://{mount.Ident}/{path}.vmdl",
			Warhead == RocketWarhead.Incendiary ? 50 : 100, null, ray.Position, ray.Forward, 400, projectile =>
			{
				projectile.Guidance = Guidance;
				projectile.Warhead = Warhead;
			} );
		if ( Guidance == RocketGuidance.RemoteControlled ) RemoteRocket = rocket;
		return rocket;
	}

	internal override void PlayLaunchSound()
	{
		base.PlayLaunchSound();
		if ( !IsProxy && Owner is { ThirdPerson: false } )
			MeleeViewPunch.Apply( Scene.Camera, new Angles( -5, 0, 0 ) );
	}

	public override bool CanPrimaryAttack() => !RemoteRocket.IsValid() && base.CanPrimaryAttack();
	public override bool CanReload() => !RemoteRocket.IsValid() && base.CanReload();
	protected override bool OnHolstering( BaseInventoryItem next ) => !RemoteRocket.IsValid() && base.OnHolstering( next );

	public override void SecondaryAttack() => ChangeGuidance();
	protected override bool WantsReload() => !Input.Down( "use" ) && base.WantsReload();

	protected override void Think()
	{
		base.Think();
		if ( Input.Down( "use" ) && Input.Pressed( "reload" ) ) ChangeWarhead();
	}

	bool CanChangePayload => IsActive && Inventory.IsValid() && !IsReloading && !GuidingRocket &&
		(Guidance != RocketGuidance.RemoteControlled || ActiveRockets == 0) && (Clip1 > 0 || Ammo1 > 0);

	[Rpc.Host( NetFlags.OwnerOnly )]
	void ChangeGuidance()
	{
		if ( !IsActive || !Inventory.IsValid() ) return;
		if ( RemoteRocket.IsValid() )
		{
			RemoteRocket.ReleaseRemoteControl();
			RemoteRocket = null;
			return;
		}
		if ( !CanChangePayload || _nextGuidanceChange > 0 ) return;

		_nextGuidanceChange = SecondaryDelay;
		Guidance = Guidance == RocketGuidance.RemoteControlled ? RocketGuidance.Unguided : Guidance + 1;
		var name = Guidance switch
		{
			RocketGuidance.LaserGuided => "Laser guided",
			RocketGuidance.RemoteControlled => "Remote controlled",
			_ => Guidance.ToString()
		};
		ShowSelection( $"{name} missile loaded" );
		ReloadPayload();
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void ChangeWarhead()
	{
		if ( !CanChangePayload || _nextWarheadChange > 0 ) return;
		_nextWarheadChange = 1;
		Warhead = Warhead == RocketWarhead.Emp ? RocketWarhead.HighExplosive : Warhead + 1;
		var name = Warhead switch
		{
			RocketWarhead.HighExplosive => "High Explosive",
			RocketWarhead.PlasmaBomb => "Plasma Bomb",
			RocketWarhead.Emp => "EMP",
			_ => "Incendiary"
		};
		ShowSelection( $"{name} warhead loaded" );
		ReloadPayload();
	}

	void ReloadPayload()
	{
		Inventory.GiveAmmo( PrimaryAmmoType, Clip1 );
		Clip1 = 0;
		Reload();
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowSelection( string text )
	{
		if ( IsProxy || Application.IsDedicatedServer ) return;
		Scene.GetSystem<MessageDisplay>()?.Show( new TitleMessage
		{
			Text = text,
			Position = new Vector2( -1, 0.5f ),
			Color = Color.White,
			HoldTime = 1,
			FadeOut = 0.25f
		} );
	}

	protected override void ModifyCamera( CameraComponent camera, ref CameraView view )
	{
		base.ModifyCamera( camera, ref view );
		if ( !IsActive || !Owner.IsValid() || !RemoteRocket.IsValid() ) return;
		view.Position = RemoteRocket.WorldPosition;
		view.Rotation = Owner.EyeTransform.Rotation;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( ViewModel?.GetComponent<SkinnedModelRenderer>() is { } renderer )
			renderer.RenderOptions.Overlay = !RemoteRocket.IsValid();
	}
}

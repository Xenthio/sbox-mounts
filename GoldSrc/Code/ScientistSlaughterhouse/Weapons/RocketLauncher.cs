using GoldSrc.HalfLife;
using GoldSrc.Maps;

namespace GoldSrc.ScientistSlaughterhouse;

internal enum RocketGuidance { Unguided, LaserGuided, Heatseeking, RemoteControlled }

[Title( "Rocket Launcher" ), Category( "Scientist Slaughterhouse" )]
[WeaponPickup( "rpg", "rockets", 50, Bucket = 3, Weight = 20, SkipAutoSelect = true, Mount = "ssh" )]
public sealed class RocketLauncher : GuidedRocketLauncher
{
	[Sync( SyncFlags.FromHost )] internal RocketGuidance Guidance { get; set; } = RocketGuidance.LaserGuided;
	[Sync( SyncFlags.FromHost )] internal Rocket RemoteRocket { get; set; }
	TimeUntil _nextGuidanceChange;

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
			100, null, ray.Position, ray.Forward, 400, projectile => projectile.Guidance = Guidance );
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
		if ( IsReloading || _nextGuidanceChange > 0 || GuidingRocket ||
			(Guidance == RocketGuidance.RemoteControlled && ActiveRockets > 0) || (Clip1 == 0 && Ammo1 == 0) ) return;

		_nextGuidanceChange = SecondaryDelay;
		Guidance = Guidance == RocketGuidance.RemoteControlled ? RocketGuidance.Unguided : Guidance + 1;
		ShowGuidance( Guidance );
		Inventory.GiveAmmo( PrimaryAmmoType, Clip1 );
		Clip1 = 0;
		Reload();
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowGuidance( RocketGuidance guidance )
	{
		if ( IsProxy || Application.IsDedicatedServer ) return;
		var name = guidance switch
		{
			RocketGuidance.LaserGuided => "Laser guided",
			RocketGuidance.RemoteControlled => "Remote controlled",
			_ => guidance.ToString()
		};
		Scene.GetSystem<MessageDisplay>()?.Show( new TitleMessage
		{
			Text = $"{name} missile loaded",
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

using GoldSrc.HalfLife;

namespace GoldSrc.HalfLifeMMod;

[Title( "Rocket Launcher" ), Category( "Half-Life: MMod" )]
[WeaponPickup( "rpg", "rockets", 5, Bucket = 3, Weight = 20, SkipAutoSelect = true, Mount = "hl1mmod" )]
public sealed class RocketLauncher : GuidedRocketLauncher
{
	bool _hasDeployed;
	int _trackingFrame;
	TimeUntil _nextTrackFrame;

	internal override string DeploySequence => _hasDeployed ? base.DeploySequence : "deploy_first";
	internal override string ReloadSequence => LaserEnabled && SpotPosition.HasValue ? "reload_screen" : "reload";
	internal override float IdleAfterReloadStart => 1.3f;
	internal override float LaserReloadDelay => 1.3f;
	public override Ray AimRay => Owner.IsValid() ? OffsetLaunchRay( new Vector3( 16, -8, -8 ) ) : base.AimRay;

	internal override void Configure()
	{
		base.Configure();
		ReloadTime = 1.3f;
	}

	protected override void OnEquipped()
	{
		DeployTime = _hasDeployed ? 0.5f : 3.33f;
		base.OnEquipped();
		_hasDeployed = true;
	}

	internal override PlayerRocket LaunchRocket( Ray ray ) =>
		NpcProjectile.Fire<MModRocket>( Owner.GameObject, "mount://hl1mmod/HL1MMod/models/rpgrocket.mdl.vmdl",
			100, null, ray.Position, ray.Forward, 250 );

	internal override void OnLaserToggled() => PlayLaserToggle( LaserEnabled );

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void PlayLaserToggle( bool enabled ) =>
		PlaySample( enabled ? "weapons/rpg_spot_on.wav" : "weapons/rpg_spot_off.wav" );

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( !IsActive || IsReloading || ViewModel?.GetComponent<SkinnedModelRenderer>() is not { } renderer ) return;

		var skin = LaserEnabled ? 5 : 0;
		if ( LaserEnabled && ActiveRockets > 0 )
		{
			if ( _nextTrackFrame <= 0 )
			{
				_trackingFrame = _trackingFrame % 4 + 1;
				_nextTrackFrame = 0.2f;
				PlaySample( "weapons/rpg_track.wav" );
			}
			skin = _trackingFrame;
		}
		else _trackingFrame = 0;

		var group = $"Skin {skin}";
		if ( renderer.MaterialGroup != group ) renderer.MaterialGroup = group;
	}

	internal override void OnViewSequenceEvent( Mdl.Event animationEvent, SkinnedModelRenderer renderer )
	{
		if ( animationEvent.Index == 5006 && int.TryParse( animationEvent.Options, out var skin ) )
			renderer.MaterialGroup = $"Skin {skin}";
	}
}

[Hide]
public sealed class MModRocket : PlayerRocket
{
	internal override GameMount EffectMount => Sandbox.Mounting.Directory.Get( "hl1mmod" ) as GameMount;
}

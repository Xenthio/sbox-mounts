using GoldSrc.HalfLife;

namespace GoldSrc.HalfLifeMMod;

[Title( "Hivehand" ), Category( "Half-Life: MMod" )]
[WeaponPickup( "hgun", "hornets", 8, ClassName = "hornetgun", Bucket = 3, Weight = 10, Mount = "hl1mmod" )]
public sealed class Hivehand : ClassicHivehand
{
	const int PodCost = 8;
	const float PodCooldown = 1;
	const string DrySample = "weapons/weapon_empty_hornetgun.wav";
	bool _hasDeployed;

	internal override string DeploySequence => _hasDeployed ? base.DeploySequence : "up_first";
	internal override float HornetDamage => 5;
	internal override float RechargeDelay => 0.5f;
	internal override float RechargeInterval => 0.25f;
	internal override float EmptyPrimaryDelay => 0;
	internal override float EmptySecondaryDelay => 0;
	internal override Vector3 WorldMuzzleOffset( bool hd ) => base.WorldMuzzleOffset( false );
	public override Ray AimRay => Owner.IsValid() ? OffsetLaunchRay( new Vector3( 16, 12, 8 ) ) : base.AimRay;

	internal override Vector2 DartOffset( int phase ) => new(
		phase is 7 or 0 or 1 ? 8 : phase is 3 or 4 or 5 ? -8 : 0,
		phase is 1 or 2 or 3 ? 8 : phase is 5 or 6 or 7 ? -8 : 0 );

	protected override void OnEquipped()
	{
		DeployTime = 0.5f;
		base.OnEquipped();
		_hasDeployed = true;
	}

	internal override void PosePickup( SkinnedModelRenderer renderer )
	{
		renderer.UseAnimGraph = false;
		renderer.Sequence.Name = "idle";
		renderer.Sequence.Looping = true;
	}

	public override void DryFire()
	{
		PlaySample( DrySample );
		base.DryFire();
	}

	protected override void Think()
	{
		base.Think();
		if ( Input.Pressed( "ToolMenu" ) && NextPrimaryFire <= 0 && NextSecondaryFire <= 0 )
			LaunchPod();
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void LaunchPod()
	{
		if ( !IsActive || !Owner.IsValid() || !IsPrimaryLaunchReady ) return;
		if ( Ammo1 < PodCost )
		{
			const float DryDelay = 0.5f;
			BlockProjectileLaunch( DryDelay );
			ShowPodAttack( false, DryDelay );
			return;
		}

		if ( ContentMount is not { } mount || mount.FindMountPath( "models/v_hgun_puke.mdl" ) is not { } path ||
			!TakePrimaryAmmo( PodCost ) ) return;

		var ray = OffsetLaunchRay( new Vector3( 16, 0, 0 ) );
		NpcProjectile.Fire<HornetPod>( Owner.GameObject, $"mount://{mount.Ident}/{path}.vmdl", 45, null, ray.Position, ray.Forward, 800 );
		ScheduleRecharge( PodCooldown );
		BlockProjectileLaunch( PodCooldown );
		ShowPodAttack( true, PodCooldown );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowPodAttack( bool launched, float delay )
	{
		if ( !IsProxy ) SetNextFire( delay );
		PlaySample( launched ? "weapons/hgun_puke.wav" : DrySample );
		if ( !launched ) return;
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( "Puke", idleAfter: IdleAfterShot );
		PostWeaponEvent( WeaponEvents.Attack );
	}
}

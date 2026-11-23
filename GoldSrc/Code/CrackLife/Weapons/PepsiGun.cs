using GoldSrc.HalfLife;

namespace GoldSrc.CrackLife;

[Title( "Pepsi Gun" ), Category( "Crack-Life: Campaign Mode" )]
[WeaponPickup( "pepsigun", "grenades", 60, Bucket = 4, Weight = 15, Mount = "clcampaign" )]
public sealed class PepsiGun : CampaignThrowable
{
	const float OpenTime = 0.6f;
	const float InsertTime = 0.5f;
	TimeUntil? _firstInsert;

	internal override string ShotSequence => "fire";
	internal override string ShotSound => "weapons/pepsigun_shoot.wav";
	internal override float? IdleAfterShot => Clip1 > 0 ? 5 : PrimaryDelay;
	internal override string ReloadSequence => "open";
	internal override float IdleAfterReloadStart => OpenTime;
	internal override int FreshPickupRounds => 12;

	internal override void Configure()
	{
		HoldType = WeaponHolds.Shotgun;
		ClipMaxSize = 8;
		PrimaryDelay = 0.75f;
		IncrementalReloading = true;
		ReloadStartTime = OpenTime + InsertTime;
		ReloadTime = InsertTime;
	}

	public override void PrimaryAttack()
	{
		if ( Owner.IsValid() && WaterBody.Find( Scene, Owner.EyePosition ).IsValid() )
		{
			PlaySample( "weapons/357_cock1.wav", 0.8f );
			SetNextPrimaryFire( 0.15f );
			return;
		}
		base.PrimaryAttack();
	}

	internal override bool Release( Ray ray )
	{
		if ( WaterBody.Find( Scene, Owner.EyePosition ).IsValid() ||
			ContentMount is not { } mount || mount.FindMountPath( "models/w_grenade.mdl" ) is not { } path ) return false;
		TimedGrenade.Spawn<TimedGrenade>( Owner.GameObject, LaunchOrigin( ray ), ray.Forward.Normal * LaunchSpeed,
			2.5f, "Pepsi Gun grenade", $"mount://{mount.Ident}/{path}.vmdl", IgnoreThrower, mount );
		return true;
	}

	public override bool CanReload() => NextPrimaryFire <= 0 && base.CanReload();

	public override void DryFire()
	{
		if ( CanReload() ) Reload();
		PlaySample( "weapons/357_cock1.wav", 0.8f );
	}

	protected override void OnReloadStarted()
	{
		base.OnReloadStarted();
		_firstInsert = OpenTime;
	}

	protected override void OnReloadInserted()
	{
		base.OnReloadInserted();
		if ( Clip1 < ClipMaxSize && Ammo1 > 0 ) PlayInsert();
	}

	void PlayInsert()
	{
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( "insert" );
		var variant = Game.Random.Int( 0, 1 ) == 0 ? "reload1" : "reload3";
		PlaySample( $"weapons/{variant}.wav", 1, Game.Random.Int( 85, 116 ) / 100f );
	}

	protected override void OnReloadFinished()
	{
		_firstInsert = null;
		base.OnReloadFinished();
		if ( IsActive ) ViewModel?.GetComponent<StudioWeaponView>()?.Play( "close", idleAfter: 1.5f );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( _firstInsert is not { } insert || insert > 0 ) return;
		_firstInsert = null;
		if ( IsReloading ) PlayInsert();
	}
}

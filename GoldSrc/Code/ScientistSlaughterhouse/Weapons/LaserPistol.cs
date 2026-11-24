using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

[Title( "Laser Pistol" ), Category( "Scientist Slaughterhouse" )]
[WeaponPickup( "9mmhandgun", "uranium", 999, ClassName = "laserhandgun", Bucket = 1, Weight = 10, PreviewPitch = 90, Mount = "ssh" )]
public sealed class LaserPistol : StudioWeapon
{
	const int MagazineSize = 24;
	const float PistolSkillDamage = 8;
	const string Glow = "mount://ssh/valve/sprites/glow01.spr";
	TimeUntil _ready;

	internal override string ShotSequence => Clip1 == 0 ? "shoot_empty" : "shoot";
	internal override string ReloadSequence => "reload";
	internal override bool CanIdle => Clip1 > 0;
	internal override float IdleAfterReloadStart => Game.Random.Float( 10, 15 );
	internal override (string Name, float? Delay) IdleAnimation => Game.Random.Float() switch
	{
		<= 0.3f => ("idle3", 49f / 16),
		<= 0.6f => ("idle1", 60f / 16),
		_ => ("idle2", 40f / 16)
	};

	internal override void Configure()
	{
		HoldType = WeaponHolds.Pistol;
		Handedness = WeaponHandedness.Right;
		ClipMaxSize = MagazineSize;
		PrimaryDelay = 0.3f;
		SecondaryDelay = 1;
		ReloadTime = 1.5f;
	}

	protected override bool OnAdding( BaseInventoryComponent inventory )
	{
		if ( Clip1 < 0 ) Clip1 = 20;
		return base.OnAdding( inventory );
	}

	public override bool HasSecondaryAmmo() => !IsHeld || Clip1 == MagazineSize;
	public override void PrimaryAttack() => FirePulse( false );
	public override void SecondaryAttack() => FirePulse( true );

	[Rpc.Host( NetFlags.OwnerOnly )]
	void FirePulse( bool charged )
	{
		if ( !IsActive || !Owner.IsValid() || _ready > 0 || IsReloading ) return;
		if ( charged && !HasSecondaryAmmo() ) return;
		if ( !TakePrimaryAmmo( charged ? MagazineSize : 1 ) ) return;

		var ray = OffsetLaunchRay( new Vector3( 0, -2, -8 ) );
		var target = ProjectileTarget.Find( Scene, Owner.GameObject, ray, range: 2048, minimumAlignment: 0.9848f );
		var direction = target.IsValid() ? (GoldSrc.HalfLife.Npc.TargetBodyPosition( target ) - ray.Position).Normal : ray.Forward;
		if ( charged )
			NpcProjectile.Fire<PinballLaser>( Owner.GameObject, Glow, PistolSkillDamage * 2.8f, null, ray.Position, direction, 1400 );
		else
			NpcProjectile.Fire<LaserPulse>( Owner.GameObject, Glow, PistolSkillDamage * 1.4f, null, ray.Position, direction, 2000 );

		_ready = charged ? SecondaryDelay : PrimaryDelay;
		ShowShot( charged, Clip1 == 0 );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowShot( bool charged, bool lastRound )
	{
		SetNextFire( charged ? SecondaryDelay : PrimaryDelay );
		PlaySample( $"weapons/pl_gun{Game.Random.Int( 1, 2 )}.wav", 0.92f, attenuation: 0.8f );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( lastRound ? "shoot_empty" : "shoot", idleAfter: Game.Random.Float( 10, 15 ) );
		if ( !IsProxy && Owner is { ThirdPerson: false } ) MeleeViewPunch.Apply( Scene.Camera, new Angles( -2, 0, 0 ) );
		PostWeaponEvent( WeaponEvents.Attack );
	}

	public override void DryFire()
	{
		if ( Clip1 == 0 && AutoReload && CanReload() ) Reload();
		else
		{
			SetNextFire( 0.2f );
			PlaySample( "weapons/357_cock1.wav", 0.8f );
		}
	}
}

using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

[Title( "Soft Metal Accelerator" ), Category( "Scientist Slaughterhouse" )]
[WeaponPickup( "desert_eagle", "uranium", 999, ClassName = "softgun", Bucket = 1, Weight = 15, Mount = "ssh" )]
public sealed class SoftMetalAccelerator : BulletWeapon
{
	internal override int FreshPickupRounds => 20;
	internal override int PlayerMuzzleFlash => 11;
	internal override Vector2 PelletSpread => Vector2.One * 0.00873f;
	internal override float IdleAfterReloadStart => Game.Random.Float( 10, 15 );
	internal override string ShotSequence => "shoot";

	internal override (string Name, float? Delay) IdleAnimation => Game.Random.Int( 0, 4 ) switch
	{
		0 => ("idle1", 2.5f),
		1 => ("idle2", 2.5f),
		2 => ("idle3", 49f / 30),
		3 => ("idle4", 2.5f),
		_ => ("idle5", 2)
	};

	internal override void Configure()
	{
		HoldType = WeaponHolds.Pistol;
		Handedness = WeaponHandedness.Right;
		ClipMaxSize = 12;
		ReloadTime = 1.5f;
		PrimaryDelay = 0.75f;
		AttackDamageTags = ["radiation", "nevergib"];
		Ballistics = new() { Damage = 16, Range = 8192, Radius = 0 };
	}

	public override Ray AimRay
	{
		get
		{
			if ( !IsHeld || !Owner.IsValid() ) return base.AimRay;

			var ray = Owner.EyeTransform.ForwardRay;
			var target = ProjectileTarget.Find( Scene, Owner.GameObject, ray, range: 8192, minimumAlignment: 0.9848f );
			return target.IsValid()
				? new Ray( ray.Position, (GoldSrc.HalfLife.Npc.TargetBodyPosition( target ) - ray.Position).Normal )
				: ray;
		}
	}

	bool IsSubmerged() => Owner.IsValid() && WaterBody.Find( Scene, Owner.EyePosition ).IsValid();

	public override void PrimaryAttack()
	{
		if ( IsSubmerged() )
		{
			Click( 0.15f );
			return;
		}

		base.PrimaryAttack();
	}

	public override void DryFire()
	{
		if ( !IsSubmerged() && AutoReload && CanReload() ) Reload();
		else Click( IsSubmerged() ? 0.15f : 0.2f );
	}

	void Click( float delay )
	{
		PlaySample( "weapons/357_cock1.wav", 0.8f );
		SetNextPrimaryFire( delay );
	}

	internal override void PlayShotSound() =>
		PlaySample( $"weapons/357_shot{Game.Random.Int( 1, 2 )}.wav", Game.Random.Float( 0.8f, 0.9f ), attenuation: 0.8f );

	protected override void OnShootEffects( ShotEffect shot )
	{
		base.OnShootEffects( shot );
		if ( !shot.NoEvents && !IsProxy && Owner is { ThirdPerson: false } )
			MeleeViewPunch.Apply( Scene.Camera, new Angles( -10, 0, 0 ) );
	}
}

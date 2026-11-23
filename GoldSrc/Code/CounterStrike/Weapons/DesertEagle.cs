using System;
using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Title( "Desert Eagle" ), Category( "Counter-Strike" )]
[WeaponPickup( "deagle", "50ae", 35, Bucket = 1, Weight = 7, Mount = "cstrike" )]
public sealed class DesertEagle : DesertEagleWeapon;

[Title( "Desert Eagle" ), Category( "Counter-Strike: Condition Zero" )]
[WeaponPickup( "deagle", "50ae", 35, Bucket = 1, Weight = 7, Mount = "czero" )]
public sealed class ConditionZeroDesertEagle : DesertEagleWeapon;

[Hide]
public abstract class DesertEagleWeapon : StudioWeapon
{
	const float DefaultAccuracy = 0.9f;
	const float RangeModifier = 0.81f;
	float _accuracy = DefaultAccuracy;
	float? _lastShot;

	internal override string ShotSequence => Clip1 == 0 ? "shoot_empty" : $"shoot{Game.Random.Int( 1, 2 )}";
	internal override float? IdleAfterShot => 1.8f;
	internal override string ShotSound => "weapons/deagle-1.wav";

	internal override void Configure()
	{
		HoldType = WeaponHolds.Pistol;
		Handedness = WeaponHandedness.Right;
		PrimaryAutomatic = false;
		PrimaryDelay = 0.225f;
		ReloadTime = 2.2f;
		ClipMaxSize = 7;
		AttackDamageTags = ["bullet"];
		Ballistics = new() { Damage = 54, Range = 4096, Radius = 0 };
	}

	public override void PrimaryAttack()
	{
		if ( IsHeld && !TakePrimaryAmmo() ) return;

		var ray = AimRay;
		var spread = Spread();
		var basis = Rotation.LookAt( ray.Forward );
		var offset = basis.Right * (SampleOffset() * spread) + basis.Up * (SampleOffset() * spread);
		var hit = BulletTrace( new Ray( ray.Position, (ray.Forward + offset).Normal ), Ballistics.Range, 0 ).Run();
		if ( hit.Hit )
			ShootBullet( hit, Ballistics.Damage * MathF.Pow( RangeModifier, hit.Distance / 500f ), 0 );

		if ( _lastShot is { } last )
			_accuracy = Math.Clamp( _accuracy - (0.4f - (Time.Now - last)) * 0.35f, 0.55f, DefaultAccuracy );
		_lastShot = Time.Now;
		TimeSinceShoot = 0;
		ShootEffects( new ShotEffect( hit.EndPosition, hit.Hit, hit.Normal, hit.GameObject, hit.Surface ) );
	}

	float Spread()
	{
		var scale = Owner switch
		{
			{ IsOnGround: false } => 1.5f,
			{ } player when player.Velocity.WithZ( 0 ).LengthSquared > 0 => 0.25f,
			{ IsDucking: true } => 0.115f,
			_ => 0.13f
		};
		return scale * (1 - _accuracy);
	}

	static float SampleOffset() => Game.Random.Float( -0.5f, 0.5f ) + Game.Random.Float( -0.5f, 0.5f );

	protected override void OnReloadStarted()
	{
		base.OnReloadStarted();
		_accuracy = DefaultAccuracy;
	}

	protected override void OnHolstered()
	{
		base.OnHolstered();
		_accuracy = DefaultAccuracy;
	}

	protected override void OnShootEffects( ShotEffect shot )
	{
		base.OnShootEffects( shot );
		if ( !IsProxy && Owner is { ThirdPerson: false } )
			MeleeViewPunch.Apply( Scene.Camera, new Angles( -2, 0, 0 ) );
	}

	public override void DryFire()
	{
		PlaySample( "weapons/dryfire_pistol.wav" );
		base.DryFire();
	}
}

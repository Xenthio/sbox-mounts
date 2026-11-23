using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

[Title( "M40A1 Sniper Rifle" ), Category( "Scientist Slaughterhouse" )]
[WeaponPickup( "m40a1", "m40", 100, ClassName = "sniper", Bucket = 2, Weight = 10, Mount = "ssh" )]
public sealed class Sniper : BulletWeapon
{
	TimeSince? _chargeAge;
	GameObject _sight;
	BeamEffect _sightBeam;

	internal override int PlayerMuzzleFlash => 41;
	internal override Vector2 PelletSpread => Vector2.Zero;
	internal override float ZoomFieldOfView => 20;
	internal override string ShotSound => "weapons/sniper_fire.wav";
	internal override string ShotSequence => Clip1 == 0 ? "firelastround" : "fire";
	internal override string ReloadSequence => "reload1";
	internal override float IdleAfterReloadStart => ReloadTime;
	internal override float? IdleAfterShot => 5;
	internal override (string Name, float? Delay) IdleAnimation => (Game.Random.Int( 0, 1 ) == 0 ? "slowidle" : "slowidle2", null);

	internal override void Configure()
	{
		HoldType = WeaponHolds.Rifle;
		ClipMaxSize = 5;
		PrimaryDelay = 1.75f;
		SecondaryDelay = 1;
		ReloadTime = 2.35f;
		AttackDamageTags = ["bullet"];
		Ballistics = new() { Damage = 12.5f, Range = 8192, Radius = 0 };
	}

	public override Ray AimRay => Owner.IsValid() ? Owner.EyeTransform.ForwardRay : base.AimRay;
	public override bool CanPrimaryAttack() => !_chargeAge.HasValue && base.CanPrimaryAttack();
	public override bool CanSecondaryAttack() => NextSecondaryFire <= 0 && !IsReloading;
	public override bool CanReload() => !_chargeAge.HasValue && base.CanReload();

	public override void SecondaryAttack()
	{
		base.SecondaryAttack();
		PlaySample( "weapons/sniper_zoom.wav" );
	}

	public override void PrimaryAttack()
	{
		if ( !HasPrimaryAmmo() || IsSubmerged() )
		{
			DryFire();
			return;
		}

		_chargeAge = 0;
		UpdateSight();
	}

	bool IsSubmerged() => Owner.IsValid() && WaterBody.Find( Scene, Owner.EyePosition ).IsValid();

	protected override void Think()
	{
		base.Think();
		if ( _chargeAge is not { } age ) return;
		if ( !IsActive || IsReloading || IsSubmerged() )
		{
			EndCharge();
			return;
		}

		if ( Input.Down( "attack1" ) )
		{
			UpdateSight();
			return;
		}

		EndCharge();
		Ballistics = Ballistics with { Damage = 25 * ((float)age).Clamp( 0.5f, 4f ) };
		base.PrimaryAttack();
		SetNextPrimaryFire( PrimaryDelay );
	}

	public override void DryFire()
	{
		PlaySample( "weapons/357_cock1.wav" );
		SetNextPrimaryFire( 0.15f );
	}

	void UpdateSight()
	{
		if ( Application.IsDedicatedServer || IsProxy ) return;
		if ( !_sight.IsValid() )
		{
			_sight = new GameObject( GameObject, true, "Sniper aiming beam" ) { NetworkMode = NetworkMode.Never };
			_sightBeam = _sight.AddComponent<BeamEffect>();
			_sightBeam.Additive = true;
			_sightBeam.BeamColor = Color.Red;
			_sightBeam.Scale = 0.5f;
		}

		var ray = AimRay;
		_sight.WorldPosition = ray.Position + ray.Forward * 10;
		_sightBeam.TargetPosition = BulletTrace( ray, Ballistics.Range, 0 ).Run().EndPosition;
	}

	void EndCharge()
	{
		_chargeAge = null;
		if ( _sight.IsValid() ) _sight.Destroy();
	}

	protected override void OnHolstered()
	{
		EndCharge();
		base.OnHolstered();
	}

	protected override void OnReloadFinished()
	{
		base.OnReloadFinished();
		if ( IsActive ) ViewModel?.GetComponent<StudioWeaponView>()?.Play( "reload2" );
	}

	internal override void DrawShotTrail( Vector3 start, Vector3 end ) { }
}

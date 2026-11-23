using GoldSrc.HalfLife;

namespace GoldSrc.HalfLifeMMod;

[Title( "Crowbar" ), Category( "Half-Life: MMod" )]
[WeaponPickup( "crowbar", null, 0, Mount = "hl1mmod" )]
public sealed class Crowbar : CrowbarWeapon
{
	TimeSince? _charging;
	int _swing;

	internal override void Configure()
	{
		HoldType = WeaponHolds.Melee;
		UsesAmmo = UsesClips = false;
		PrimaryDelay = 0.5f;
		SecondaryDelay = 1.13f;
		SecondaryAutomatic = false;
		AttackDamageTags = ["club"];
		Ballistics = new() { Damage = 10, Range = 32, Radius = 0 };
	}

	public override bool CanPrimaryAttack() => _charging is null && base.CanPrimaryAttack();
	public override bool CanSecondaryAttack() => _charging is null && base.CanSecondaryAttack();

	public override void PrimaryAttack() => Swing( 10, PrimaryDelay, false );
	public override void SecondaryAttack() => Swing( 15, SecondaryDelay, true );

	protected override void Think()
	{
		base.Think();
		if ( Input.Pressed( "ToolMenu" ) && CanPrimaryAttack() && CanSecondaryAttack() ) BeginCharge();
		if ( _charging is not null && !Input.Down( "ToolMenu" ) ) ReleaseCharge();
	}

	void BeginCharge()
	{
		_charging = 0;
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( "charge", holdLastFrame: true );
	}

	void ReleaseCharge()
	{
		if ( _charging is not { } elapsed ) return;
		_charging = null;
		Swing( System.MathF.Max( 10, (float)elapsed * 10 ), 1.07f, true, charged: true );
	}

	void Swing( float damage, float delay, bool heavy, bool charged = false )
	{
		var hit = TraceSwing( AimRay );
		if ( hit.Hit ) ShootBullet( hit, damage, 0, hitboxTags: false );
		SetNextFire( delay );
		PostWeaponEvent( WeaponEvents.Attack );

		var variant = _swing++ % 3 + 1;
		var sequence = charged ? (hit.Hit ? "charge_hit" : "charge_miss")
			: heavy ? $"heavy_{(hit.Hit ? "hit" : "miss")}{variant}"
			: (variant, hit.Hit) switch
			{
				(1, true) => "attack1",
				(1, false) => "attack1miss",
				(_, true) => $"attack{variant}hit",
				_ => $"attack{variant}"
			};
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( sequence );

		var shot = new ShotEffect( hit.EndPosition, hit.Hit, hit.Normal, hit.GameObject, hit.Surface );
		if ( !hit.Hit ) PlaySample( "weapons/cbar_miss1.wav" );
		else if ( HitFlesh( shot ) )
			PlaySample( heavy ? "weapons/cbar_hhitbod.wav" : "weapons/cbar_hitbod" );
		else
		{
			PlaySample( heavy ? "weapons/cbar_hhit.wav" : "weapons/cbar_hit" );
			ImpactDecal.Place( hit.EndPosition, hit.Normal, $"shot{Game.Random.Int( 1, 5 )}", 10 );
		}
	}

	protected override void OnHolstered()
	{
		_charging = null;
		base.OnHolstered();
	}
}

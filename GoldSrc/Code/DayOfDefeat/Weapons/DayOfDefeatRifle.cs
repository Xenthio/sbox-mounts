using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Hide]
public abstract class DayOfDefeatRifle : DayOfDefeatFirearm
{
	static readonly BBox MeleeHull = new( new Vector3( -16, -16, -36 ), new Vector3( 16, 16, 36 ) );

	internal override float? IdleAfterShot => PrimaryDelay;
	internal override int BrassBodyGroup => 2;
	internal abstract string MeleeSequence { get; }
	internal virtual bool HasBayonet => false;

	internal override void Configure()
	{
		base.Configure();
		HoldType = WeaponHolds.Rifle;
		Ballistics = Ballistics with { Range = 8192 };
		PrimaryDelay = 0.4f;
		SecondaryDelay = 1.1f;
	}

	public override bool CanSecondaryAttack() => NextSecondaryFire <= 0 && !IsReloading;

	public override bool CanReload() => NextPrimaryFire <= 0 && base.CanReload();

	public override void PrimaryAttack()
	{
		if ( HasPrimaryAmmo() ) SetNextSecondaryFire( PrimaryDelay );
		base.PrimaryAttack();
	}

	public override void SecondaryAttack()
	{
		if ( !Owner.IsValid() ) return;
		SetNextFire( SecondaryDelay );
		var trace = TraceLineOfFire( Owner.EyeTransform.ForwardRay, 64, 0 );
		var hit = trace.Run();
		if ( !hit.Hit ) hit = trace.Size( MeleeHull ).Run();

		if ( hit.Hit )
			ShootBullet( hit, 60, 0, HasBayonet ? ["slash", "nevergib"] : ["club", "nevergib"], hitboxTags: false );

		var flesh = hit.Hit && hit.GameObject?.GetComponentInParent<IDamageable>() is not null;
		PlayMelee( flesh );
	}

	[Rpc.Broadcast]
	void PlayMelee( bool flesh )
	{
		PostWeaponEvent( WeaponEvents.Attack );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( MeleeSequence, idleAfter: SecondaryDelay );
		PlaySample( HasBayonet ? "weapons/knifeswing2.wav" : "weapons/knifeswing.wav" );
		if ( flesh ) PlaySample( HasBayonet ? "weapons/knife_hit" : "weapons/butt_hit.wav" );
	}
}

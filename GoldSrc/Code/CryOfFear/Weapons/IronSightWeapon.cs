using GoldSrc.HalfLife;

namespace GoldSrc.CryOfFear;

[Hide]
public abstract class IronSightWeapon : BulletWeapon
{
	const float SightTransitionTime = 0.3f;
	TimeUntil? _reloadAfterSights;

	internal virtual string RaiseSightsSequence => "iron_to";
	internal virtual string LowerSightsSequence => "iron_from";
	internal override float ZoomFieldOfView => 50;

	public override bool CanReload() => IsActive && NextPrimaryFire <= 0 && base.CanReload();
	public override bool CanSecondaryAttack() => IsActive && NextPrimaryFire <= 0 && base.CanSecondaryAttack();

	public override void SecondaryAttack()
	{
		base.SecondaryAttack();
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( Zoomed ? RaiseSightsSequence : LowerSightsSequence, idleAfter: SightTransitionTime );
		SetNextFire( SightTransitionTime );
	}

	public override void Reload()
	{
		if ( !CanReload() ) return;
		if ( !Zoomed )
		{
			base.Reload();
			return;
		}

		// Finish the owner's sight transition before asking the host to start reloading.
		SecondaryAttack();
		_reloadAfterSights = SightTransitionTime;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( _reloadAfterSights is not { } reload || reload > 0 ) return;
		_reloadAfterSights = null;
		Reload();
	}

	protected override void OnHolstered()
	{
		_reloadAfterSights = null;
		base.OnHolstered();
	}
}

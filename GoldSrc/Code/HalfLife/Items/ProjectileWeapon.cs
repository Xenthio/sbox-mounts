namespace GoldSrc.HalfLife;

[Hide]
public abstract class ProjectileWeapon : StudioWeapon
{
	TimeUntil _nextLaunch, _nextSecondary;
	protected bool IsPrimaryLaunchReady => _nextLaunch <= 0;

	protected void BlockProjectileLaunch( float delay )
	{
		_nextLaunch = delay;
		_nextSecondary = delay;
	}

	public override void PrimaryAttack() => Launch( AimRay );

	[Rpc.Host( NetFlags.OwnerOnly )]
	void Launch( Ray ray ) => TryLaunch( ray, Release );

	protected bool TryLaunch( Ray ray, System.Func<Ray, bool> release, bool secondary = false, float extraDelay = 0,
		bool keepIdleDeadline = false, int ammoCost = 1, float? primaryCooldown = null )
	{
		if ( !IsActive || !Owner.IsValid() || (secondary ? _nextSecondary : _nextLaunch) > 0 || !ray.Position.IsFinite || !ray.Forward.IsFinite ||
			ray.Position.Distance( Owner.EyePosition ) > 96 || !HasPrimaryAmmo() ) return false;
		if ( ammoCost > 1 && (UsesPrimaryClip ? Clip1 : Ammo1) < ammoCost ) return false;
		if ( !release( ray ) ) return false;
		TakePrimaryAmmo( ammoCost );
		var delay = (secondary ? SecondaryDelay : PrimaryDelay) + extraDelay;
		_nextLaunch = primaryCooldown ?? delay;
		if ( secondary ) _nextSecondary = delay;
		ShowLaunch( ShotSequence, IdleAfterShot, keepIdleDeadline );
		return true;
	}

	internal abstract bool Release( Ray ray );

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowLaunch( string sequence, float? idleAfter, bool keepIdleDeadline )
	{
		if ( ViewModel?.GetComponent<StudioWeaponView>() is { } view )
		{
			if ( keepIdleDeadline ) idleAfter = view.RemainingIdleTime;
			if ( sequence is not null ) view.Play( sequence, idleAfter: idleAfter );
			else if ( idleAfter.HasValue ) view.RemainingIdleTime = idleAfter.Value;
		}
		PlayLaunchSound();
		PostWeaponEvent( WeaponEvents.Attack );
	}

	internal virtual void PlayLaunchSound()
	{
		if ( ShotSound is { } sound ) PlaySample( sound );
	}
}

using System;
using GoldSrc.HalfLife;
using GoldSrc.Maps;

namespace GoldSrc.CrackLife;

[Title( "Needle" ), Category( "Crack-Life: Campaign Mode" )]
[WeaponPickup( "needle", null, 0, Bucket = 0, Weight = 0, Mount = "clcampaign" )]
public sealed class Needle : StudioWeapon
{
	TimeUntil _nextInjection;

	internal override (string Name, float? Delay) IdleAnimation => ("idle", null);
	internal override string ShotSequence => "giveshot";
	internal override string ShotSound => "weapons/needleshot.wav";

	internal override void Configure()
	{
		UsesAmmo = UsesClips = false;
		PrimaryAutomatic = false;
		PrimaryDelay = 60;
		HoldType = WeaponHolds.Item;
	}

	public override bool CanSecondaryAttack() => false;

	public override void PrimaryAttack()
	{
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( ShotSequence );
		ShootEffects();
		Inject();
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void Inject()
	{
		if ( !IsActive || !Owner.IsValid() || _nextInjection > 0 ||
			MapState.PlayerVitals( Owner ) is not { Health: > 0 } vitals ) return;

		_nextInjection = PrimaryDelay;
		if ( vitals.Health < vitals.MaxHealth )
			vitals.Health = MathF.Min( vitals.MaxHealth, vitals.Health + 25 );
	}
}

using System;
using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Hide]
public abstract class DayOfDefeatGrenadeWeapon : PinGrenadeWeapon
{
	internal const float FreshFuse = 5;
	internal override string PinSequence => "pinpull";
	internal override string PinSound => "weapons/grenpinpull.wav";
	internal override string ThrowSound => "weapons/grenthrow.wav";
	internal override (string Name, float? Delay) IdleAnimation => ("idle", 10);

	internal override void Configure()
	{
		base.Configure();
		StartingAmmo = 1;
		DeployTime = 0.75f;
		PrimaryDelay = 1;
	}

	public override bool CanSecondaryAttack() => IsActive && NextSecondaryFire <= 0 && NextPrimaryFire <= 0 && CanPrime;
	public override void SecondaryAttack() => Prime( true );

	internal override Vector3 LaunchOrigin( Vector3 direction )
	{
		var start = Underhand ? Owner.WorldPosition + Vector3.Up * (Owner.IsDucking ? 18 : 36) : Owner.EyePosition;
		return TraceLineOfFire( new Ray( start, direction ), 16, 0 ).WithoutTags( "player", "npc" ).Run().EndPosition;
	}

	internal Vector3 LaunchVelocity( Vector3 direction, float pitch ) => Underhand
		? direction * 300
		: direction * (Math.Clamp( (90 - pitch) * 4, 0, 600 ) + Owner.Velocity.Dot( direction ));

	internal override string LaunchGrenade( Vector3 origin, Vector3 direction, float pitch, float pinAge )
	{
		DayOfDefeatGrenade.Throw( Owner.GameObject, origin, LaunchVelocity( direction, pitch ), FreshFuse, Definition.Model, Underhand );
		return "throw";
	}
}

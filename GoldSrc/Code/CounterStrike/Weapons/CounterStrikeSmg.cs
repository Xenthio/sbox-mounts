using System;
using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Hide]
public abstract class CounterStrikeSmg : CounterStrikeRifle
{
	internal readonly record struct Kick( float Up, float Side, float UpPerShot, float SidePerShot, float MaxUp, float MaxSide, int DirectionChange );
	internal readonly record struct KickPattern( Kick Airborne, Kick Moving, Kick Crouched, Kick Standing );

	bool _kickRight;

	internal abstract KickPattern Kickback { get; }
	internal virtual bool QuadraticAccuracy => false;
	internal virtual bool IntegerAccuracyDivision => true;
	internal virtual float ShotAttenuation => 0.64f;
	internal override float MovingSpreadSpeed => float.PositiveInfinity;
	internal override float MovingPenalty => 0;
	internal override float ReloadAccuracy => InitialAccuracy;
	internal override bool ResetAccuracyWhenIdle => false;
	internal override string BrassModel => "models/pshell.mdl";
	internal override (string Name, float? Delay) IdleAnimation => ("idle1", 20);
	internal override float? IdleAfterShot => 2;

	internal override void Configure()
	{
		base.Configure();
		DeployTime = 0.75f;
		HoldType = WeaponHolds.Smg;
	}

	internal override float FiringAccuracy( int shots )
	{
		var power = QuadraticAccuracy ? shots * shots : shots * shots * shots;
		var growth = IntegerAccuracyDivision ? power / (int)AccuracyDivisor : power / AccuracyDivisor;
		return MathF.Min( MaxAccuracy, growth + AccuracyOffset );
	}

	internal override void PlayShotSound()
	{
		if ( Application.IsDedicatedServer || ContentMount is not { } mount ) return;
		NpcAudio.Play( mount.NpcAudio.Find( ShotSound ), WorldPosition, pitch: Game.Random.Int( 94, 109 ) / 100f, attenuation: ShotAttenuation );
	}

	public override Ray AimRay
	{
		get
		{
			var ray = base.AimRay;
			if ( IsProxy || Owner is not { ThirdPerson: false } ) return ray;
			var punch = MeleeViewPunch.Current( Scene.Camera );
			var angles = Rotation.LookAt( ray.Forward ).Angles() + punch;
			return new Ray( ray.Position, angles.ToRotation().Forward );
		}
	}

	internal override void ApplyFiringRecoil( PlayerController player, int shots )
	{
		var pattern = Kickback;
		var kick = player switch
		{
			{ IsOnGround: false } => pattern.Airborne,
			_ when player.Velocity.WithZ( 0 ).LengthSquared > 0 => pattern.Moving,
			{ IsDucking: true } => pattern.Crouched,
			_ => pattern.Standing
		};
		var additional = shots == 1 ? 0 : shots;
		var current = MeleeViewPunch.Current( Scene.Camera );
		var pitch = MathF.Max( -kick.MaxUp, current.pitch - kick.Up - additional * kick.UpPerShot );
		var side = kick.Side + additional * kick.SidePerShot;
		var yaw = Math.Clamp( current.yaw + (_kickRight ? side : -side), -kick.MaxSide, kick.MaxSide );
		MeleeViewPunch.Apply( Scene.Camera, new Angles( pitch, yaw, 0 ) );

		if ( Game.Random.Int( 0, kick.DirectionChange ) == 0 ) _kickRight = !_kickRight;
	}
}

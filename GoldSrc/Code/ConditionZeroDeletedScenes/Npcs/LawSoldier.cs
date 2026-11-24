using System;
using GoldSrc.HalfLife;

namespace GoldSrc.ConditionZeroDeletedScenes;

[Hide]
public abstract class LawSoldier : CampaignSoldier
{
	const string Fire = "LAW_fire";
	const string Reload = "LAW_Reload";
	const string RunClip = "law_run";
	const float FireInterval = 5;

	[Sync( SyncFlags.FromHost )] bool Loaded { get; set; } = true;
	TimeUntil _nextLaunch;

	internal override int WeaponBodyGroup => 2;
	internal override float AttackRange => WeaponRange > 0 ? WeaponRange : base.AttackRange;
	internal override Vector3 AttackOrigin => WorldPosition + Vector3.Up * 60;
	internal override string[] ExtraActions => [Fire, Reload, RunClip];

	internal override StudioSequence MovementSequence( float speed ) =>
		(speed > 12 ? Find( RunClip ) : null) ?? base.MovementSequence( speed );

	internal override bool Pursue( Vector3 goal )
	{
		var pace = Find( RunClip ) is { GroundSpeed: > 0 } run ? run.GroundSpeed / Definition.Speed : 1;
		return !HoldPosition && MoveToward( goal, 130, pace );
	}

	internal override bool InAttackRange( GameObject target, Vector3 aim )
	{
		var offset = aim - AttackOrigin;
		return offset.LengthSquared <= AttackRange * AttackRange && WorldRotation.Forward.Dot( offset.Normal ) >= 0.5f;
	}

	internal override void BeginAttack( GameObject target )
	{
		if ( !Loaded )
		{
			if ( Find( Reload ) is { } reload ) PlayAction( reload );
			return;
		}
		if ( _nextLaunch <= 0 && InAttackRange( target, AimAt( target ) ) && Find( Fire ) is { } fire ) AttackWith( target, fire );
	}

	internal override void HandleActionEvent( int id )
	{
		if ( id == 2 && string.Equals( Action, Reload, StringComparison.OrdinalIgnoreCase ) ) Loaded = true;
	}

	internal override void PlayAttackSound( Vector3 origin ) { }

	internal override void FireProjectile( GameObject target, Vector3 origin, Vector3 direction )
	{
		if ( !Loaded || _nextLaunch > 0 || string.IsNullOrEmpty( ProjectilePath ) ) return;

		var aim = Rotation.LookAt( direction );
		var spread = SpreadFor( 0 );
		if ( spread > 0 )
		{
			var x = Game.Random.Float( -0.5f, 0.5f ) + Game.Random.Float( -0.5f, 0.5f );
			var y = Game.Random.Float( -0.5f, 0.5f ) + Game.Random.Float( -0.5f, 0.5f );
			direction = (direction + spread * (aim.Right * x + aim.Up * y)).Normal;
		}

		// Spawn at the original gun position. The motor keeps this axis for the rest of the flight.
		NpcProjectile.Fire<LawRocket>( GameObject, ProjectilePath, Definition.Damage, null, origin, direction, null );
		PlayGunshot( "weapons/law_fire.wav", origin, pitch: Game.Random.Int( 94, 109 ) * 0.01f, attenuation: 0.4f );
		Loaded = false;
		_nextLaunch = FireInterval;
	}

	internal override SavedMagazine CaptureMagazine() => new( Loaded ? 1 : 0, MathF.Max( _nextLaunch, 0 ) );
	internal override void RestoreMagazine( SavedMagazine magazine )
	{
		Loaded = magazine.Rounds > 0;
		_nextLaunch = magazine.ShotDelay;
	}
}

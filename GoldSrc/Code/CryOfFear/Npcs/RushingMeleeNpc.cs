using GoldSrc.HalfLife;

namespace GoldSrc.CryOfFear;

[Hide]
public abstract class RushingMeleeNpc : CryOfFearMeleeNpc
{
	const float ClawReach = 55;
	const float BulletDamageScale = 0.3f;

	internal abstract string ChaseSequence { get; }
	internal virtual string StrikeVoice => null;
	internal virtual string StrikeDamageTag => null;
	internal override string WallSounds => SwingSounds;

	internal override bool Pursue( Vector3 goal )
	{
		var gait = Find( ChaseSequence );
		var pace = gait is { GroundSpeed: > 0 } ? gait.GroundSpeed / Definition.Speed : 1;
		return MoveToward( goal, AttackRange * 0.5f, pace );
	}

	internal override void ModifyDamage( Component.IDamageable.DamageEvent damage )
	{
		base.ModifyDamage( damage );
		if ( damage.DamageInfo.Tags.Has( "bullet" ) ) damage.AcceptedDamage *= BulletDamageScale;
	}

	internal override void HandleActionEvent( int id )
	{
		if ( id is not (1 or 2 or 3) ) return;

		var victim = TraceMelee( ClawReach, Definition.Damage, "slash", out var contact, StrikeDamageTag );
		var sound = victim.IsValid() ? HitSounds : contact ? WallSounds : SwingSounds;
		EmitSound( sound, WorldPosition, pitch: Game.Random.Int( 95, 105 ) / 100f );
		if ( StrikeVoice is { } voice && Game.Random.Int( 0, 1 ) == 1 )
			EmitSound( voice, WorldPosition, pitch: VoicePitch, speaker: this );

		if ( victim?.GameObject.GetComponent<PlayerController>() is { } player )
		{
			var push = id switch
			{
				1 => -WorldRotation.Right * 100,
				2 => WorldRotation.Right * 100,
				_ => -WorldRotation.Forward * 100
			};
			PunchPlayer( player, push, new Angles( 5, 0, id == 3 ? 0 : id == 2 ? 18 : -18 ) );
		}
	}
}

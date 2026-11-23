using GoldSrc.HalfLife;

namespace GoldSrc.CryOfFear;

[Hide]
public abstract class CryOfFearMeleeNpc : VoicedNpc
{
	internal virtual string SwingSounds => "slower/hammer_miss";
	internal virtual string HitSounds => "slower/hammer_strike";
	internal virtual string WallSounds => "slower/hammer_strike_wall";
	internal virtual StudioSequence SelectMeleeSequence( GameObject target ) => WeightedActivity( Melee );

	internal override IEnumerable<StudioSequence> ExtraSequences => Activities( Melee );

	internal override void OnModelLoaded( ModelLoader loader )
	{
		if ( loader.EyePosition.z > 0 ) EyeHeight = loader.EyePosition.z;
	}

	internal override string MapModel( Bsp.Entity entity ) => entity.GetString( "model" );

	internal override bool InAttackRange( GameObject target, Vector3 aim ) =>
		WorldPosition.DistanceSquared( target.WorldPosition ) <= AttackRange * AttackRange;

	internal override void IdleMovement()
	{
		if ( Movement?.MoveTarget.HasValue == true ) Movement.Stop();
	}

	internal override void BeginAttack( GameObject target )
	{
		var facing = (target.WorldPosition - WorldPosition).WithZ( 0 ).Normal;
		if ( facing.IsNearlyZero() || WorldRotation.Forward.Dot( facing ) < 0.7f ) return;
		if ( SelectMeleeSequence( target ) is not { } melee ) return;

		EmitSound( Sounds.Attack, WorldPosition, speaker: this );
		PlayAction( melee );
	}

	internal override void HandleActionEvent( int id )
	{
		if ( id is not (1 or 2 or 3) ) return;
		var victim = TraceMelee( AttackRange, Definition.Damage, "club", out var contact );
		var sound = victim.IsValid() ? HitSounds : contact ? WallSounds : SwingSounds;
		EmitSound( sound, WorldPosition );
		if ( victim?.GameObject.GetComponent<PlayerController>() is { } player )
			PunchPlayer( player, Vector3.Zero, new Angles( 5, 0, id == 3 ? 0 : id == 2 ? 18 : -18 ) );
	}
}

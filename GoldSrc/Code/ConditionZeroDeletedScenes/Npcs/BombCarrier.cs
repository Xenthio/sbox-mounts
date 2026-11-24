using GoldSrc.HalfLife;

namespace GoldSrc.ConditionZeroDeletedScenes;

[Hide]
public abstract class BombCarrier : CampaignSoldier
{
	const string DetonationSequence = "kamakazi";
	const string RunSequence = "pistol_run";

	bool _detonating;

	internal override int WeaponBodyGroup => 9;
	internal override string[] ExtraActions => [DetonationSequence, RunSequence];

	internal override void ApplyEquipment()
	{
		base.ApplyEquipment();
		if ( Renderer.Model.Parts.All.Any( part => part.Name == "bombs" && part.Choices.Count > 1 ) )
			Renderer.SetBodyGroup( "bombs", 1 );
	}

	internal override StudioSequence MovementSequence( float speed ) =>
		(speed > 12 ? Find( RunSequence ) : null) ?? base.MovementSequence( speed );

	internal override bool Pursue( Vector3 goal )
	{
		var pace = Find( RunSequence ) is { GroundSpeed: > 0 } run ? run.GroundSpeed / Definition.Speed : 1;
		return !HoldPosition && MoveToward( goal, AttackRange * 0.5f, pace );
	}

	internal override bool InAttackRange( GameObject target, Vector3 aim ) =>
		(target.WorldPosition - WorldPosition).WithZ( 0 ).LengthSquared < AttackRange * AttackRange;

	internal override void BeginAttack( GameObject target )
	{
		if ( InAttackRange( target, target.WorldPosition ) && Find( DetonationSequence ) is { } animation )
			PlayAction( animation );
	}

	internal override void FinishAction( string name )
	{
		// The original detonates on the schedule after the animation, rather than at studio event 12.
		if ( name != DetonationSequence || Health <= 0 ) return;
		_detonating = true;
		var damage = new DamageInfo( Health + 50, GameObject, GameObject )
		{
			Origin = WorldPosition,
			Position = WorldPosition
		};
		damage.Tags.Add( "alwaysgib" );
		((IDamageable)this).OnDamage( damage );
	}

	protected override void OnDeath( Component.IDamageable.DeathEvent death )
	{
		base.OnDeath( death );
		if ( !_detonating ) return;

		var floor = Scene.Trace.Ray( WorldPosition + Vector3.Up * 8, WorldPosition + Vector3.Down * 32 )
			.IgnoreGameObjectHierarchy( GameObject ).WithoutTags( "npc", "player", "trigger", "debris" ).Run();
		var mount = Sandbox.Mounting.Directory.Get( "czeror" ) as GameMount;
		Blast.Detonate( GameObject, GameObject, floor.Hit ? floor.EndPosition : WorldPosition, floor.Normal, Definition.Damage, floor.Hit, mount );
	}
}

using GoldSrc.HalfLife;

namespace GoldSrc.ConditionZeroDeletedScenes;

[Hide]
public abstract class MacheteSoldier : CampaignSoldier
{
	const float Reach = 128;
	const float FacingDot = 0.7f;

	static readonly string[] Attacks =
		["machete_melee", "machete_melee2", "machete_melee3", "machete_melee4", "machete_melee5", "machete_melee6"];

	const string CombatIdle = "machete_combatidle";
	const string RunClip = "machete_run";
	const string WalkClip = "machete_patrol_street";

	static readonly string[] Reactions =
		["machete_smflinch", "machete_leftarmflinch", "machete_rightarmflinch", "machete_leftlegsmflinch", "machete_rightlegsmflinch"];

	[Sync( SyncFlags.FromHost )] bool Alert { get; set; }
	GameObject _swingTarget;

	internal override int WeaponBodyGroup => 6;
	internal override Vector3 AttackOrigin => WorldPosition + Vector3.Up * 54;
	internal override string[] ExtraActions => [.. Attacks, .. Reactions, CombatIdle, RunClip, WalkClip];
	internal override StudioSequence RestSequence => Alert ? Find( CombatIdle ) ?? base.RestSequence : base.RestSequence;
	internal override StudioSequence MovementSequence( float speed ) => speed <= 12 ? null
		: Find( speed >= Definition.Speed * 0.8f ? RunClip : WalkClip ) ?? base.MovementSequence( speed );
	internal override float IdleMovementPace => Find( WalkClip ) is { GroundSpeed: > 0 } walk
		? walk.GroundSpeed / Definition.Speed : base.IdleMovementPace;

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( Networking.IsHost ) Alert = InCombat;
	}

	internal override bool InAttackRange( GameObject target, Vector3 aim )
	{
		var offset = (target.WorldPosition - WorldPosition).WithZ( 0 );
		return offset.LengthSquared <= AttackRange * AttackRange && WorldRotation.Forward.Dot( offset.Normal ) >= FacingDot;
	}

	internal override void BeginAttack( GameObject target )
	{
		if ( !InAttackRange( target, target.WorldPosition ) ) return;
		if ( Find( Attacks[Game.Random.Int( 0, Attacks.Length - 1 )] ) is not { } swing ) return;
		_swingTarget = target;
		PlayAction( swing );
	}

	internal override void FinishAction( string name )
	{
		if ( Attacks.Contains( name ) ) _swingTarget = null;
	}

	internal override void HandleActionEvent( int id )
	{
		if ( id != 3 || !Attacks.Contains( Action ) ) return;

		// The original traces a duck hull from three-quarters height toward the enemy's lower body.
		var origin = AttackOrigin;
		var target = _swingTarget.IsValid() ? _swingTarget : LastHostile;
		var direction = target.IsValid()
			? (target.WorldPosition + Vector3.Up * (TargetHullBounds( target ).Size.z * 0.25f) - origin).Normal
			: WorldRotation.Forward;
		var damage = Maps.SuitItem.Skill switch { 1 => 15, 3 => 35, _ => 25 };
		var victim = TraceMelee( new Ray( origin, direction ), Reach, damage, "club", out _, useHitboxes: false );
		var push = WorldRotation.Forward * 100 + Vector3.Up * 50;
		if ( victim?.GetComponent<PlayerController>() is { } player )
			PunchPlayer( player, push, new Angles( 5, 0, 0 ) );
		else if ( victim.IsValid() )
			PushVictim( victim, push );
	}

	internal override StudioSequence SelectFlinch( Component.IDamageable.DamageEvent damage )
	{
		if ( Action is not null ) return null;
		var groups = HitGroups( damage.DamageInfo );
		var clip = groups.Has( "hitgroup_4" ) ? Reactions[1]
			: groups.Has( "hitgroup_5" ) ? Reactions[2]
			: groups.Has( "hitgroup_6" ) ? Reactions[3]
			: groups.Has( "hitgroup_7" ) ? Reactions[4] : Reactions[0];
		return Find( clip ) ?? base.SelectFlinch( damage );
	}

	internal override StudioSequence ChooseDeathSequence( ITagSet groups, Vector3 direction )
	{
		var dot = WorldRotation.Forward.Dot( direction );
		var clip = IsHeadHit( groups ) ? "machete_headshot"
			: groups.Has( "hitgroup_3" ) ? "machete_diegutshot"
			: dot > 0.3f ? "machete_dieforward"
			: dot <= -0.3f ? "machete_dieback1" : "machete_die-simple";
		return Find( clip ) ?? base.ChooseDeathSequence( groups, direction );
	}
}

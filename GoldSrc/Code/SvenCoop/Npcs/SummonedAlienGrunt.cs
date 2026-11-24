using System;
using GoldSrc.HalfLife;

namespace GoldSrc.SvenCoop;

[Title( "Sven Alien Grunt" ), Category( "Sven Co-op" )]
[NpcDefinition( "monster_alien_grunt", "agrunt", MonsterClass.AlienMilitary, 150, 32, 64, 125,
	Mount = "svencoop", ModelVariants = ["agruntf"], ArmorGroup = 10, Attack = NpcAttack.Projectile, Damage = 10,
	Range = 1024, ProjectileModel = "hornet", Gibs = GibSet.Alien, Blood = Npc.BloodColor.Yellow )]
public sealed class SummonedAlienGrunt : AlienGrunt
{
	[Property] public bool ThrowsSnarkNests { get; set; } = true;
	[Property] public bool MeleeOnly { get; set; }
	[Property, Sync( SyncFlags.FromHost )] public bool Friendly { get; set; }

	const int ThrowEvent = 14;
	readonly List<NestSnark> _brood = [];
	SnarkNest _nest;
	TimeUntil _nextNest;
	Vector3 _tossVelocity;
	int _throwAction = -1;

	internal override MonsterClass Classification => Friendly ? MonsterClass.PlayerAlly : base.Classification;
	internal override IEnumerable<StudioSequence> ExtraSequences => [.. base.ExtraSequences, .. Activities( Ranged + 1 )];
	Vector3 TossOrigin => WorldPosition + WorldRotation.Forward * 64 + WorldRotation.Right * 48 + Vector3.Up * 24;

	internal override void ApplyMapProperties( Bsp.Entity entity )
	{
		var weapons = entity.GetInt( "weapons" );
		ThrowsSnarkNests = weapons == 0 || (weapons & 2) != 0;
		MeleeOnly = (weapons & 8) != 0;
		Friendly = entity.GetInt( "is_player_ally" ) != 0 || entity.GetInt( "classify" ) == (int)MonsterClass.PlayerAlly;
	}

	internal override string MapModel( Bsp.Entity entity ) => entity.GetString( "new_model" ) is { Length: > 0 } custom
		? custom : Friendly ? "models/agruntf.mdl" : null;

	internal override bool InAttackRange( GameObject target, Vector3 aim ) =>
		base.InAttackRange( target, aim ) && (!MeleeOnly || ThrowsSnarkNests || WorldPosition.Distance( target.WorldPosition ) <= 100);

	internal override void BeginAttack( GameObject target )
	{
		if ( WorldPosition.Distance( target.WorldPosition ) > 100 && TryNestAttack( target ) ) return;
		if ( MeleeOnly && WorldPosition.Distance( target.WorldPosition ) > 100 )
		{
			Pursue( target.WorldPosition );
			return;
		}
		base.BeginAttack( target );
	}

	internal override bool TryOccludedAttack( GameObject target ) => TryNestAttack( target );

	bool TryNestAttack( GameObject target )
	{
		if ( !ThrowsSnarkNests || _nextNest > 0 || _nest.IsValid() || !IsLiveTarget( target ) ) return false;
		_brood.RemoveAll( snark => !snark.IsValid() || snark.Health <= 0 );
		if ( _brood.Count > 0 || WeightedActivity( Ranged + 1 ) is not { } sequence ||
			!sequence.Events.Any( animationEvent => animationEvent.Index == ThrowEvent ) ) return false;

		if ( Game.Random.Float( 0, 10 ) < 8 )
		{
			_nextNest = 5;
			return false;
		}

		var end = TargetHullCenter( target ).WithZ( target.WorldPosition.z + 2 );
		if ( !TryToss( target, TossOrigin, end, out _tossVelocity ) )
		{
			_nextNest = 1;
			return false;
		}

		_nextNest = 3;
		Movement?.Stop();
		PlayAction( sequence );
		_throwAction = ActionSerial;
		return true;
	}

	bool TryToss( GameObject target, Vector3 start, Vector3 end, out Vector3 velocity )
	{
		velocity = Vector3.Zero;
		var gravity = -Scene.PhysicsWorld.Gravity.z;
		if ( gravity <= 0 || end.z - start.z > 500 ) return false;
		var midpoint = (start + end) * 0.5f;
		var trace = Scene.Trace.Ray( midpoint, midpoint + Vector3.Up * 500 )
			.IgnoreGameObjectHierarchy( GameObject ).IgnoreGameObjectHierarchy( target )
			.WithoutTags( "trigger", "debris" ).Run();
		var apex = trace.EndPosition.z - 15;
		if ( apex <= MathF.Max( start.z, end.z ) ) return false;
		var rise = MathF.Sqrt( 2 * (apex - start.z) / gravity );
		var fall = MathF.Sqrt( 2 * (apex - end.z) / gravity );
		if ( rise < 0.1f ) return false;

		var horizontal = (end - start).WithZ( 0 ) / (rise + fall);
		var peak = (start + horizontal * rise).WithZ( apex );
		var path = Scene.Trace.Ray( start, peak ).IgnoreGameObjectHierarchy( GameObject )
			.IgnoreGameObjectHierarchy( target ).WithoutTags( "trigger", "debris" );
		if ( path.Run().Hit || path.FromTo( peak, end ).Run().Hit ) return false;

		velocity = horizontal + Vector3.Up * (gravity * rise);
		return true;
	}

	internal override void HandleActionEvent( int id )
	{
		if ( id != ThrowEvent )
		{
			base.HandleActionEvent( id );
			return;
		}
		if ( _throwAction != ActionSerial ) return;

		_throwAction = -1;
		_nest = SnarkNest.Throw( this, TossOrigin, _tossVelocity );
		_nextNest = _nest.IsValid() ? Game.Random.Float( 5, 20 ) : 3;
	}

	internal void RegisterHatchling( NestSnark snark ) => _brood.Add( snark );
}

using GoldSrc.HalfLife;
using Sandbox.Npcs.Layers;

namespace GoldSrc.SvenCoop;

[Title( "Stukabat" ), Category( "Sven Co-op" )]
[NpcDefinition( "monster_stukabat", "stukabat", MonsterClass.AlienPredator, 123, 12, 24, 400,
	Mount = "svencoop", Attack = NpcAttack.Melee, Damage = 12, Range = 56,
	Movement = NpcMovement.Flying, IdleSequence = "Hover", Blood = BloodColor.Yellow )]
public sealed partial class Stukabat : Npc
{
	const string CeilingLanding = "Land_ceiling";
	const string GroundLanding = "Land_ground";
	const string TakeOff = "Take_off_from_land";
	// The studio clip still has its old name. Its server animation event delivers the bite.
	const string Bite = "Attack_bomb";
	const int BiteEvent = 1;
	static readonly string[] FlightActions =
	[
		CeilingLanding, GroundLanding, TakeOff, Bite, "Flying_cycler", "Dive_cycler",
		"Subtle_fidget", "Preen_fidget", "Swing_fidget", "Subtle_fidget_on_ground",
		"Disply_fidget_on_ground", "Death_fall_simple", "Die_on_ground"
	];

	enum Roost { None, Ceiling, Ground }
	[Sync( SyncFlags.FromHost )] Roost Perched { get; set; }
	[Sync( SyncFlags.FromHost )] bool Diving { get; set; }
	Vector3? _landing;
	Roost _landingSide;
	TimeUntil _nextRoost = 5;

	internal override string[] ExtraActions => FlightActions;
	internal override float HoverClearance => 40;
	internal override float ViewDot => 0.5f;
	internal override StudioSequence RestSequence => Perched switch
	{
		Roost.Ceiling => Find( "Subtle_fidget" ),
		Roost.Ground => Find( "Disply_fidget_on_ground" ),
		_ => base.RestSequence
	};

	internal override string MapModel( Bsp.Entity entity ) => entity.GetString( "new_model" );
	internal override void OnModelLoaded( ModelLoader loader ) => EyeHeight = loader.EyePosition.z;
	internal override void PlaceAtSpawn()
	{
		base.PlaceAtSpawn();
		if ( Movement is not FlyingLayer flight ) return;
		flight.HoverHeight = 0;
		flight.Bounds = FlyingLayer.FlyingBounds.AirOnly;
	}

	internal override StudioSequence MovementSequence( float speed )
	{
		if ( Perched != Roost.None || speed <= 20 ) return null;
		return Find( Diving ? "Dive_cycler" : "Flying_cycler" );
	}

	internal override void IdleMovement()
	{
		if ( SearchForEnemy() ) return;
		Diving = false;
		if ( Perched != Roost.None ) return;
		if ( _landing is { } destination )
		{
			if ( WorldPosition.DistanceSquared( destination ) <= 25 )
			{
				Movement?.Stop();
				WorldPosition = destination;
				if ( Movement is FlyingLayer flight ) flight.Velocity = Vector3.Zero;
				var sequence = Find( _landingSide == Roost.Ceiling ? CeilingLanding : GroundLanding );
				if ( sequence is not null ) PlayAction( sequence );
				_landing = null;
			}
			else if ( Movement?.Status == MovementLayer.MovementStatus.Failed )
			{
				_landing = null;
				Movement.Stop();
				_nextRoost = 5;
			}
			return;
		}
		if ( _nextRoost > 0 ) return;
		_nextRoost = 5;
		var destinationAbove = FindRoost( Vector3.Up, 160, Definition.Height + 1 );
		_landingSide = destinationAbove.HasValue ? Roost.Ceiling : Roost.Ground;
		_landing = destinationAbove ?? FindRoost( Vector3.Down, 96, 1 );
		if ( _landing is { } perch ) Movement?.MoveTo( perch, 4, 120 );
	}

	Vector3? FindRoost( Vector3 direction, float distance, float clearance )
	{
		// Use the edge of the flying hull to reject perches that only fit its centre.
		var half = new Vector3( Definition.Radius, Definition.Radius, 1 );
		var start = WorldPosition + Vector3.Up * (direction.z > 0 ? Definition.Height - 1 : 1);
		var result = Scene.Trace.Box( new BBox( -half, half ), start, start + direction * distance )
			.IgnoreGameObjectHierarchy( GameObject ).WithoutTags( "npc", "player", "trigger", "debris" ).Run();
		if ( !result.Hit || result.StartedSolid || result.Normal.Dot( -direction ) < 0.8f ) return null;
		return result.EndPosition + direction * (1 - clearance);
	}

	internal override void Engage( GameObject target )
	{
		_landing = null;
		_nextRoost = 5;
		Animation.SetLookTarget( target );
		if ( Perched != Roost.None )
		{
			var state = Perched;
			Perched = Roost.None;
			_landingSide = Roost.None;
			var sequence = Find( state == Roost.Ceiling ? CeilingLanding : TakeOff );
			if ( sequence is not null ) PlaySequence( sequence, state == Roost.Ceiling );
			return;
		}

		var aim = TargetBodyPosition( target );
		RememberEnemy( target, aim - Vector3.Up * (Definition.Height * 0.5f) );
		var offset = aim - AttackOrigin;
		if ( offset.Length <= AttackRange && WorldRotation.Forward.Dot( offset.Normal ) >= 0.5f &&
			CanHit( target, aim ) && Find( Bite ) is { } bite )
		{
			Diving = false;
			ClearAirRoute();
			Movement?.Stop();
			PlayAction( bite );
			return;
		}

		FlyAfterEnemy( target );
	}

	internal override void FinishAction( string name )
	{
		if ( name is CeilingLanding or GroundLanding ) Perched = _landingSide;
	}

	internal override void HandleActionEvent( int id )
	{
		if ( id != BiteEvent ) return;
		var victim = TraceMelee( 80, Definition.Damage, "poison", out _ );
		if ( victim.IsValid() )
			EmitSound( "headcrab/hc_headbite.wav", WorldPosition, 0.6f, Game.Random.Float( 1.4f, 1.6f ), attenuation: 2 );
		else
			EmitSound( "zombie/claw_miss2.wav", WorldPosition, attenuation: 0.8f );
	}

	internal override void PlaySequenceSound( NpcAudio audio, int id )
	{
		if ( id == 9 ) NpcAudio.Play( audio.Find( "zombie/claw_miss1.wav" ), WorldPosition, attenuation: 0.8f );
	}

	internal override StudioSequence ChooseDeathSequence( ITagSet groups, Vector3 direction ) =>
		Find( Perched == Roost.Ground ? "Die_on_ground" : "Death_fall_simple" );
}

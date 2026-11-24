using GoldSrc.HalfLife;
using Sandbox.Npcs;

namespace GoldSrc.SvenCoop;

[Title( "Chumtoad" ), Category( "Sven Co-op" )]
[NpcDefinition( "monster_chumtoad", "chumtoad", MonsterClass.AlienMonster, 100, 8, 12, 158,
	Mount = "svencoop", Attack = NpcAttack.Shock, Range = 300, Damage = 10, Blood = BloodColor.Yellow )]
public sealed class Chumtoad : Npc, Component.IPressable
{
	internal const string Prefab = "mount://svencoop/entities/npcs/monster_chumtoad.prefab";
	const int StandUp = 19, GasWiggle = 24, LieDown = 58;
	const int NoUse = 256;
	const float FollowDistance = 128;
	const float GasRadius = 320;

	[Property, Sync( SyncFlags.FromHost )] public int ClassOverride { get; set; }
	[Property, Sync( SyncFlags.FromHost )] public bool CanFollow { get; set; } = true;
	[Property, Sync( SyncFlags.FromHost )] public GameObject PetOwner { get; set; }
	[Sync( SyncFlags.FromHost )] GameObject Leader { get; set; }
	[Sync( SyncFlags.FromHost )] bool Running { get; set; }
	[Sync( SyncFlags.FromHost )] bool ReleasingGas { get; set; }
	[Sync( SyncFlags.FromHost )] int BlinkFrame { get; set; }

	TimeUntil _nextBlink;
	TimeUntil _nextGas, _nextPulse, _nextCloud, _gasEnds;
	GameObject _previousOwner;

	internal override MonsterClass Classification => ClassOverride switch
	{
		_ when PetOwner.IsValid() => MonsterClass.PlayerAlly,
		-1 => MonsterClass.None,
		>= 1 and <= 13 => (MonsterClass)ClassOverride,
		_ => base.Classification
	};

	internal override float ViewDot => 0.2f;
	internal override int EffectiveSkin => BlinkFrame > 0 ? BlinkFrame : base.EffectiveSkin;
	internal override IEnumerable<StudioSequence> ExtraSequences =>
		[.. Activities( Idle ), Activity( LieDown ), Activity( GasWiggle ), Activity( StandUp )];
	internal override StudioSequence RestSequence => ReleasingGas ? Activity( GasWiggle ) : base.RestSequence;
	internal override StudioSequence MovementSequence( float speed ) => speed > 12 ? Activity( Running ? Run : Walk ) : null;
	internal override string MapModel( Bsp.Entity entity ) => entity.GetString( "new_model" );

	internal override void OnModelLoaded( ModelLoader loader ) => EyeHeight = 20;

	protected override void OnStart()
	{
		if ( Networking.IsHost ) UpdateOwner();
		base.OnStart();
	}

	internal override void ApplyMapProperties( Bsp.Entity entity )
	{
		CanFollow = (entity.GetInt( "spawnflags" ) & NoUse) == 0;
		ClassOverride = entity.GetInt( "classify" );
		if ( ClassOverride == 0 && entity.GetInt( "is_player_ally" ) != 0 )
			ClassOverride = (int)MonsterClass.PlayerAlly;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( !Renderer.IsValid() || !Renderer.Model.IsValid() || Health <= 0 ) return;

		if ( Networking.IsHost && _nextBlink <= 0 )
		{
			_nextBlink = 0.1f;
			if ( BlinkFrame > 0 ) BlinkFrame--;
			else if ( Game.Random.Int( 0, 127 ) == 0 ) BlinkFrame = 2;
		}
		var skin = $"Skin {EffectiveSkin}";
		if ( Renderer.MaterialGroup != skin && Renderer.Model.GetMaterialGroupIndex( skin ) >= 0 )
			Renderer.MaterialGroup = skin;
	}

	internal override void FinishMovementCycle()
	{
		if ( Action is null && !ReleasingGas && Movement?.MoveTarget is null &&
			WeightedActivity( Idle ) is { } fidget && fidget != RestSequence )
			PlayAction( fidget );
	}

	bool AcceptsLeader( GameObject player ) => Health > 0 && player.IsValid() &&
		(PetOwner.IsValid() ? player == PetOwner : CanFollow && GetDisposition( player ) == Disposition.Friendly);

	IPressable.Tooltip? IPressable.GetTooltip( IPressable.Event e )
	{
		var player = e.Source.GameObject;
		if ( !AcceptsLeader( player ) ) return null;
		return Leader == player
			? new IPressable.Tooltip( "#npc.action.stop_following", "person_off", null )
			: new IPressable.Tooltip( "#npc.action.follow", "follow_the_signs", null );
	}

	bool IPressable.CanPress( IPressable.Event e ) => AcceptsLeader( e.Source.GameObject );

	bool IPressable.Press( IPressable.Event e )
	{
		ToggleFollow( e.Source.GameObject );
		return true;
	}

	[Rpc.Host]
	void ToggleFollow( GameObject source )
	{
		var player = source?.GetComponentInParent<PlayerController>();
		if ( !player.IsValid() || !AcceptsLeader( player.GameObject ) || WorldPosition.Distance( player.WorldPosition ) > 160 ||
			(Rpc.Caller is not null && player.Network.Owner != Rpc.Caller) ) return;

		Leader = Leader == player.GameObject ? null : player.GameObject;
		Movement?.Stop();
	}

	internal override bool FollowLeader()
	{
		if ( !IsLiveTarget( Leader ) ) return false;

		var distance = WorldPosition.Distance( Leader.WorldPosition );
		if ( distance >= 270 ) Running = true;
		else if ( distance < 190 ) Running = false;

		if ( distance > FollowDistance ) HopToward( Leader.WorldPosition, FollowDistance, Running );
		else
		{
			Movement?.Stop();
			Animation.SetLookTarget( Leader );
		}
		return true;
	}

	internal override void IdleMovement()
	{
		Running = false;
		base.IdleMovement();
	}

	bool HopToward( Vector3 point, float stop, bool run )
	{
		Running = run;
		var gait = Activity( run ? Run : Walk );
		var pace = gait is { GroundSpeed: > 0 } ? gait.GroundSpeed / Definition.Speed : 1;
		return MoveToward( point, stop, pace );
	}

	internal override bool Pursue( Vector3 goal ) => HopToward( goal, 130, true );
	internal override bool InAttackRange( GameObject target, Vector3 aim ) => WorldPosition.Distance( target.WorldPosition ) < AttackRange;

	internal override void BeginAttack( GameObject target )
	{
		if ( _nextGas <= 0 && Activity( LieDown ) is { } lieDown ) PlayAction( lieDown );
	}

	internal override void FinishAction( string name )
	{
		if ( name != Activity( LieDown )?.Name ) return;
		ReleasingGas = true;
		_gasEnds = 3;
		_nextGas = 12;
		_nextPulse = 0;
		_nextCloud = 0;
	}

	void UpdateOwner()
	{
		if ( _previousOwner == PetOwner ) return;
		_previousOwner = PetOwner;
		Leader = PetOwner;
		SvenPetCollision.SetIgnoresPlayers( GameObject, PetOwner.IsValid() );
	}

	protected override void Think()
	{
		UpdateOwner();
		if ( !ReleasingGas )
		{
			base.Think();
			return;
		}

		if ( _gasEnds <= 0 )
		{
			ReleasingGas = false;
			if ( Activity( StandUp ) is { } standUp ) PlayAction( standUp );
			return;
		}
		if ( _nextPulse > 0 ) return;
		_nextPulse = 0.1f;
		if ( _nextCloud <= 0 )
		{
			_nextCloud = 5;
			EmitSound( "doors/aliendoor1.wav", WorldPosition );
			EmitSound( "ambience/disgusting.wav", WorldPosition );
			ChumtoadGas.Show( WorldPosition );
		}
		if ( !DamageNearbyEnemies() ) return;
		_gasEnds = 3;
		_nextGas = 12;
	}

	internal override StudioSequence SelectFlinch( Component.IDamageable.DamageEvent damage ) =>
		ReleasingGas ? null : base.SelectFlinch( damage );

	internal void Retire()
	{
		if ( GetComponent<RevivableNpc>() is { } revival ) revival.Allowed = false;
		var damage = new DamageInfo { Damage = Health, Origin = WorldPosition, Position = WorldPosition };
		damage.Tags.Add( "nevergib" );
		((IDamageable)this).OnDamage( damage );
	}

	bool DamageNearbyEnemies()
	{
		var origin = WorldPosition + Vector3.Up * 12;
		var victims = Scene.FindInPhysics( new Sphere( origin, GasRadius ) )
			.Select( obj => obj.GetComponentInParent<IDamageable>() ).OfType<Component>().Distinct().ToArray();
		var hit = false;
		foreach ( var victim in victims )
		{
			if ( !victim.IsValid() || victim == this || !IsLiveTarget( victim.GameObject ) ||
				(PetOwner.IsValid() && !SvenPetCollision.IsNotPlayer( victim )) ||
				ClassOf( victim.GameObject ) == MonsterClass.Machine || GetDisposition( victim.GameObject ) != Disposition.Hostile ) continue;

			var point = TargetHullCenter( victim.GameObject );
			var damage = Definition.Damage * (1 - origin.Distance( point ) / GasRadius);
			if ( damage <= 0 ) continue;

			var info = new DamageInfo { Damage = damage, Origin = origin, Position = point, Attacker = GameObject, Weapon = GameObject };
			info.Tags.Add( "nervegas" );
			info.Tags.Add( "nevergib" );
			((IDamageable)victim).OnDamage( info );
			hit = true;
		}
		return hit;
	}
}

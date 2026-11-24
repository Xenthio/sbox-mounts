using System;
using GoldSrc.HalfLife;
using GoldSrc.Maps;

namespace GoldSrc.ConditionZeroDeletedScenes;

[Title( "Deleted Scenes Civilian" )]
[NpcDefinition( "monster_npc", "npc-scientist", MonsterClass.HumanPassive, 50, 16, 72, 200,
	Mount = "czeror", Blood = Npc.BloodColor.Red, Gibs = GibSet.Human, IdleSequence = "idle1" )]
public sealed class CampaignCivilian : TalkingNpc, Component.IPressable
{
	[Property, Hide] public int Head { get; set; } = -1;
	[Property, Hide] public string IdleClip { get; set; }
	[Property, Hide] public string DeathClip { get; set; }
	[Property, Hide] public string UseTarget { get; set; }
	[Property, Hide] public string DeathTrigger { get; set; }
	[Property, Hide] public string Language { get; set; } = "am";
	[Property, Hide] public bool HoldPosition { get; set; }
	[Property, Hide] public bool Wander { get; set; }
	[Property, Hide] public bool FleeDanger { get; set; } = true;
	[Property, Hide] public bool SilentDeath { get; set; }
	[Property, Hide] public bool Invulnerable { get; set; }
	[Property, Hide] public bool Unkillable { get; set; }

	[Sync( SyncFlags.FromHost )] GameObject Leader { get; set; }
	TimeUntil _nextUse, _nextMove, _nextPain;

	internal override float ViewDot => -0.7f;
	internal override string[] ExtraActions => [IdleClip, DeathClip];
	internal override StudioSequence RestSequence => Find( IdleClip ) ?? base.RestSequence;
	internal override StudioSequence ChooseDeathSequence( ITagSet groups, Vector3 direction ) =>
		Find( DeathClip ) ?? base.ChooseDeathSequence( groups, direction );

	internal override void ApplyMapProperties( Bsp.Entity entity )
	{
		Head = entity.GetInt( "head", -1 );
		IdleClip = entity.GetString( "idleanim" );
		DeathClip = entity.GetString( "deathanim" );
		UseTarget = entity.GetString( "usetarget" );
		DeathTrigger = entity.GetString( "deathtrigger" );
		Language = entity.GetString( "language" )?.ToLowerInvariant() ?? "am";
		HoldPosition = entity.GetInt( "cantmove" ) != 0;
		Wander = entity.GetInt( "wander" ) != 0;
		FleeDanger = entity.GetInt( "fleeondanger", 1 ) != 0;
		SilentDeath = entity.GetInt( "silentdeath" ) != 0;
		Invulnerable = entity.GetInt( "invulnerable" ) != 0;
		Unkillable = entity.GetInt( "unkillable" ) != 0;
		var multiplier = entity.GetFloat( "healthmult", 1 );
		if ( multiplier > 0 ) Health *= multiplier;
		if ( HoldPosition || entity.GetInt( "dontfall" ) != 0 ) HoldMapPosition();
	}

	internal override string MapModel( Bsp.Entity entity ) => entity.GetString( "model" );

	internal override void ApplyEquipment()
	{
		var heads = Renderer.Model.Parts.All.FirstOrDefault( part => part.Name == "heads" );
		if ( heads is not null && Head >= 0 && Head < heads.Choices.Count )
			Renderer.SetBodyGroup( heads.Name, Head );
	}

	protected override void Think()
	{
		if ( AdvanceAction() || HoldPosition || _nextMove > 0 ) return;
		_nextMove = 0.25f;

		var threat = FleeDanger ? FindVisibleThreat() ?? FindVisibleEnemy() : null;
		if ( threat.IsValid() )
		{
			var away = (WorldPosition - threat.WorldPosition).WithZ( 0 ).Normal;
			MoveToward( WorldPosition + away * 200, 16 );
			return;
		}
		if ( IsLiveTarget( Leader ) )
		{
			if ( WorldPosition.DistanceSquared( Leader.WorldPosition ) > 128 * 128 )
				MoveToward( Leader.WorldPosition, 128 );
			else
			{
				Movement?.Stop();
				Animation.SetLookTarget( Leader );
			}
			return;
		}

		Leader = null;
		if ( Wander ) IdleMovement();
		else if ( Movement?.IsMoving == true ) Movement.Stop();
	}

	bool HasUseTarget => !string.IsNullOrEmpty( UseTarget ) && UseTarget != "none";
	bool CanUse => Health > 0 && (HasUseTarget || (string.IsNullOrEmpty( UseTarget ) && !HoldPosition));
	bool IPressable.CanPress( IPressable.Event e ) => CanUse;

	IPressable.Tooltip? IPressable.GetTooltip( IPressable.Event e )
	{
		if ( !CanUse ) return null;
		if ( HasUseTarget ) return new( "Talk", "chat", null );
		return Leader == e.Source.GameObject
			? new( "#npc.action.stop_following", "person_off", null )
			: new( "#npc.action.follow", "follow_the_signs", null );
	}

	bool IPressable.Press( IPressable.Event e )
	{
		if ( !CanUse ) return false;
		Use( e.Source.GameObject );
		return true;
	}

	[Rpc.Host]
	void Use( GameObject source )
	{
		var player = source?.GetComponentInParent<PlayerController>();
		if ( !CanUse || !player.IsValid() || player.WorldPosition.DistanceSquared( WorldPosition ) > 160 * 160 ||
			(Rpc.Caller is not null && (player.Network.Owner ?? Connection.Local) != Rpc.Caller) ) return;

		if ( HasUseTarget )
		{
			GetComponent<Monster>()?.FireAnimationTarget( UseTarget );
			return;
		}

		if ( _nextUse > 0 ) return;
		_nextUse = 1;
		Leader = Leader == player.GameObject ? null : player.GameObject;
		if ( !Leader.IsValid() ) Movement?.Stop();
		Speak( "npc", Leader.IsValid() ? "ok" : "waithere" );
	}

	internal override void ModifyDamage( Component.IDamageable.DamageEvent damage )
	{
		if ( Invulnerable )
		{
			damage.Cancelled = true;
			return;
		}
		base.ModifyDamage( damage );
		if ( Unkillable ) damage.AcceptedDamage = Math.Clamp( damage.AcceptedDamage, 0, MathF.Max( 0, Health - 1 ) );
	}

	protected override void OnDeath( Component.IDamageable.DeathEvent death )
	{
		var actor = GetComponent<Monster>();
		if ( actor.IsValid() && !string.IsNullOrEmpty( DeathTrigger ) && DeathTrigger != actor.DeathTarget )
			actor.FireAnimationTarget( DeathTrigger );
		base.OnDeath( death );
	}

	internal override void PainSound()
	{
		if ( _nextPain > 0 ) return;
		_nextPain = Game.Random.Float( 0.5f, 0.75f );
		Speak( "h", $"pain{Game.Random.Int( 1, 3 )}" );
	}

	internal override void DeathSound()
	{
		if ( !SilentDeath ) Speak( "h", $"death{Game.Random.Int( 1, 3 )}" );
	}

	void Speak( string group, string sample )
	{
		var file = ModelPath?.Split( '/' ).LastOrDefault();
		var voice = file is "us_gal.mdl.vmdl" or "schoolgirl.mdl.vmdl" ? "f" : "m";
		EmitSound( $"{group}/{group}_{voice}_{sample}_{Language}.wav", WorldPosition, speaker: this );
	}
}

using GoldSrc.HalfLife;
using GoldSrc.Maps;

namespace GoldSrc.ConditionZeroDeletedScenes;

[Title( "Deleted Scenes Hostage" )]
[NpcDefinition( "monster_hostage", "npc-scientist", MonsterClass.HumanPassive, 100, 16, 72, 200,
	Mount = "czeror", Blood = Npc.BloodColor.Red, IdleSequence = "idle1" )]
public sealed class CampaignHostage : TalkingNpc, Component.IPressable
{
	const float FollowDistance = 80;

	[Property, Hide] public int Head { get; set; } = -1;
	[Property, Hide] public string IdleClip { get; set; }
	[Property, Hide] public string DeathClip { get; set; }
	[Property, Hide] public string RescueClip { get; set; }
	[Property, Hide] public string RescueTarget { get; set; }
	[Property, Hide] public bool NoAutoRemove { get; set; }
	[Property, Hide] public string Language { get; set; } = "am";
	[Sync( SyncFlags.FromHost )] public bool Rescued { get; private set; }
	[Sync( SyncFlags.FromHost )] GameObject Leader { get; set; }
	[Sync( SyncFlags.FromHost )] bool HasFollowed { get; set; }

	TimeUntil _nextUse, _nextFollow, _nextRescueCheck, _nextPain;

	internal override string[] ExtraActions => [IdleClip, DeathClip, RescueClip];
	internal override StudioSequence RestSequence => !HasFollowed && !Rescued ? Find( IdleClip ) ?? base.RestSequence : base.RestSequence;
	internal override StudioSequence ChooseDeathSequence( ITagSet groups, Vector3 direction ) =>
		Find( DeathClip ) ?? base.ChooseDeathSequence( groups, direction );

	internal override void ApplyMapProperties( Bsp.Entity entity )
	{
		Head = entity.GetInt( "head", -1 );
		IdleClip = entity.GetString( "idleanim" );
		DeathClip = entity.GetString( "deathanim" );
		RescueClip = entity.GetString( "rescueanim" );
		RescueTarget = entity.GetString( "rescuetarget" );
		NoAutoRemove = entity.GetInt( "noautoremove" ) != 0;
		var language = entity.GetString( "language" )?.ToLowerInvariant();
		if ( language is "am" or "br" or "fr" or "ge" or "ja" or "ru" ) Language = language;
	}

	internal override string MapModel( Bsp.Entity entity ) => entity.GetString( "model" );

	internal override void ApplyEquipment()
	{
		var heads = Renderer.Model.Parts.All.FirstOrDefault( part => part.Name == "heads" );
		if ( heads is not null && Head >= 0 && Head < heads.Choices.Count )
			Renderer.SetBodyGroup( heads.Name, Head );
	}

	bool CanUse => !Rescued && Health > 0;
	bool IPressable.CanPress( IPressable.Event e ) => CanUse;
	IPressable.Tooltip? IPressable.GetTooltip( IPressable.Event e ) => !CanUse ? null
		: Leader == e.Source.GameObject ? new( "#npc.action.stop_following", "person_off", null )
		: new( "#npc.action.follow", "follow_the_signs", null );

	bool IPressable.Press( IPressable.Event e )
	{
		if ( !CanUse ) return false;
		ToggleFollow( e.Source.GameObject );
		return true;
	}

	[Rpc.Host]
	void ToggleFollow( GameObject source )
	{
		var player = source?.GetComponentInParent<PlayerController>();
		if ( !CanUse || _nextUse > 0 || !player.IsValid() || player.WorldPosition.DistanceSquared( WorldPosition ) > 160 * 160 ||
			(Rpc.Caller is not null && (player.Network.Owner ?? Connection.Local) != Rpc.Caller) ) return;

		_nextUse = 1;
		if ( Leader == player.GameObject )
		{
			Leader = null;
			Movement?.Stop();
			Speak( "waithere", "waithere", "npc" );
			return;
		}

		Leader = player.GameObject;
		HasFollowed = true;
		_nextFollow = 0;
		Speak( "getmeout", "help" );
	}

	protected override void Think()
	{
		if ( AdvanceAction() || Rescued || _nextFollow > 0 ) return;
		_nextFollow = 0.2f;
		if ( !IsLiveTarget( Leader ) )
		{
			Leader = null;
			if ( Movement?.IsMoving == true ) Movement.Stop();
			return;
		}

		var distance = WorldPosition.Distance( Leader.WorldPosition );
		if ( distance < FollowDistance )
		{
			Movement?.Stop();
			Animation.SetLookTarget( Leader );
			return;
		}

		var speed = distance < 110 ? distance : distance < 250 ? 300 : 400;
		MoveToward( Leader.WorldPosition, FollowDistance, speed / Definition.Speed );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( !Networking.IsHost || !CanUse || _nextRescueCheck > 0 ) return;
		_nextRescueCheck = 0.1f;
		var actor = GetComponent<Monster>();
		if ( Scene.GetAllComponents<HostageRescuePoint>().Any( point => point.Map == actor?.Map && point.Contains( WorldPosition ) ) )
			Rescue( actor );
	}

	void Rescue( Monster actor )
	{
		Rescued = true;
		Leader = null;
		Movement?.Stop();
		InterruptAction();
		Animation.ClearLookTarget();

		if ( !string.IsNullOrEmpty( RescueTarget ) ) actor?.FireAnimationTarget( RescueTarget );
		else Speak( "thanksave", "thankf" );
		if ( !IsValid || NoAutoRemove ) return;
		if ( !PlayAnimation( RescueClip ) ) GameObject.Destroy();
	}

	internal override void FinishAction( string name )
	{
		if ( Rescued && !NoAutoRemove && name == RescueClip ) GameObject.Destroy();
	}

	internal override void PainSound()
	{
		if ( _nextPain > 0 ) return;
		_nextPain = 1;
		var sample = $"pain{Game.Random.Int( 1, 3 )}";
		Speak( sample, sample );
	}

	internal override void DeathSound()
	{
		var sample = $"death{Game.Random.Int( 1, 3 )}";
		Speak( sample, sample );
	}

	void Speak( string male, string female, string group = "h" )
	{
		var file = ModelPath?.Split( '/' ).LastOrDefault();
		var woman = file is "us_gal.mdl.vmdl" or "schoolgirl.mdl.vmdl";
		var voice = woman ? $"f_{female}" : $"m_{male}";
		EmitSound( $"{group}/{group}_{voice}_{Language}.wav", WorldPosition, speaker: this );
	}

	internal sealed record SavedEscort( bool Followed, bool FollowingPlayer, bool Rescued );
	internal SavedEscort CaptureEscort( PlayerController player ) => new( HasFollowed, Leader.IsValid() && Leader == player?.GameObject, Rescued );
	internal void RestoreEscort( SavedEscort saved, PlayerController player )
	{
		if ( saved is null ) return;
		HasFollowed = saved.Followed;
		Leader = saved.FollowingPlayer ? player?.GameObject : null;
		Rescued = saved.Rescued;
	}
}

using GoldSrc.HalfLife;
using GoldSrc.Maps;

namespace GoldSrc.ConditionZeroDeletedScenes;

[Title( "Deleted Scenes Sequence" )]
public sealed class TriggerSequence : MapEntity
{
	[Property] public string SequenceFile { get; set; }
	[Property] public string SequenceId { get; set; }
	[Property] public bool Once { get; set; }

	ConditionZeroDeletedScenesMount _mount;
	CampaignSequences.Program _program;
	GameObject _activator;
	UseType _use;
	float _value;
	float _nextAt;
	int _index;
	bool _used;

	public override void Trigger( GameObject activator = null ) => StartSequence( activator, UseType.Toggle, 0 );
	public override void TurnOn( GameObject activator = null ) => StartSequence( activator, UseType.On, 0 );
	public override void TurnOff( GameObject activator = null ) => StartSequence( activator, UseType.Off, 0 );
	public override void SetValue( float value, GameObject activator = null ) => StartSequence( activator, UseType.Set, value );

	void StartSequence( GameObject activator, UseType type, float value )
	{
		if ( !Networking.IsHost || !CanTrigger || !Map.IsValid() || (Once && _used) || !LoadProgram() ) return;

		_activator = activator;
		_use = type;
		_value = value;
		_index = 0;
		_nextAt = Time.Now;
		_used = true;
	}

	bool LoadProgram()
	{
		_mount = Sandbox.Mounting.Directory.Get( "czeror" ) as ConditionZeroDeletedScenesMount;
		_program = _mount?.Sequences.Find( SequenceFile, SequenceId );
		if ( _program is { Cues.Length: > 0 } ) return true;
		_program = null;
		return false;
	}

	internal sealed record SavedSequence( bool Used, bool Running, int Index, float Delay, int Activator, bool PlayerActivated, UseType Use, float Value );

	internal SavedSequence CaptureSequence( PlayerController player ) => new( _used, _program is not null, _index,
		System.MathF.Max( 0, _nextAt - Time.Now ), _activator?.GetComponentInParent<MapEntity>()?.EntityIndex ?? -1,
		player.IsValid() && _activator == player.GameObject, _use, _value );

	internal void RestoreSequence( SavedSequence saved, PlayerController player )
	{
		if ( saved is null ) return;
		_used = saved.Used;
		_program = null;
		_activator = null;
		if ( !saved.Running || !LoadProgram() ) return;

		_index = System.Math.Clamp( saved.Index, 0, _program.Cues.Length );
		_nextAt = Time.Now + System.MathF.Max( 0, saved.Delay );
		_activator = saved.PlayerActivated ? player?.GameObject : saved.Activator < 0 ? null
			: Map.Entities.FirstOrDefault( entity => entity.IsValid() && entity.EntityIndex == saved.Activator )?.GameObject;
		_use = saved.Use;
		_value = saved.Value;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost || !CanTrigger ) return;
		for ( var i = 0; i < 64 && _program is { } program && Time.Now >= _nextAt; i++ )
		{
			if ( _index >= program.Cues.Length )
			{
				_program = null;
				_activator = null;
				break;
			}
			var cue = program.Cues[_index++];
			_nextAt += cue.Pause;
			Play( program.Source, cue );
			if ( _program == program && cue.Repeat ) _index = 0;
		}
	}

	void Play( string source, CampaignSequences.Cue cue )
	{
		if ( !string.IsNullOrEmpty( cue.FireTargets ) ) Fire( Map.BuildOutput( cue.FireTargets, _use ), _activator, _value );
		if ( !CanTrigger ) return;
		if ( !string.IsNullOrEmpty( cue.KillTargets ) ) Fire( Map.BuildOutput( null, killName: cue.KillTargets ), _activator );
		if ( !CanTrigger ) return;
		if ( !string.IsNullOrEmpty( cue.Text.Text ) ) ShowText( cue.Text );
		if ( string.IsNullOrEmpty( cue.Sound ) ) return;

		if ( string.IsNullOrEmpty( cue.Speaker ) || cue.Speaker == "none" )
		{
			_mount.NpcAudio.EmitSequenceSpeech( source, cue.Sound, WorldPosition, null );
			return;
		}
		var speaker = Map.FindAll( cue.Speaker ).OfType<Monster>().Select( actor => actor.Npc )
			.OfType<VoicedNpc>().FirstOrDefault( npc => npc.IsValid() && npc.Health > 0 );
		if ( !speaker.IsValid() ) return;

		var listener = Map.Find( cue.Listener )?.GameObject;
		if ( !listener.IsValid() && string.Equals( cue.Listener, "player", System.StringComparison.OrdinalIgnoreCase ) )
			listener = Scene.GetAllComponents<PlayerController>().MinBy( player => player.WorldPosition.DistanceSquared( speaker.WorldPosition ) )?.GameObject;
		if ( listener.IsValid() ) speaker.Animation.SetLookTarget( listener );
		_mount.NpcAudio.EmitSequenceSpeech( source, cue.Sound, speaker.WorldPosition, speaker );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void ShowText( TitleMessage message )
	{
		if ( !Application.IsDedicatedServer )
			Game.ActiveScene.GetSystem<MessageDisplay>()?.Show( message );
	}
}

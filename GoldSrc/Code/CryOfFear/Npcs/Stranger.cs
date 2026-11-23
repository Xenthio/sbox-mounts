using GoldSrc.HalfLife;
using GoldSrc.Maps;

namespace GoldSrc.CryOfFear;

[Title( "Stranger" ), Category( "Cry of Fear" )]
[NpcDefinition( "monster_stranger", "stranger", MonsterClass.AlienMonster, 70, 16, 72, 32.39f,
	Mount = "cof", Attack = NpcAttack.Melee, Damage = 2, Range = 250, Blood = Npc.BloodColor.Red )]
public sealed class Stranger : CryOfFearMeleeNpc
{
	const string VoiceSamplePath = "stranger/st_voiceloop.wav";
	[Sync( SyncFlags.FromHost )] bool VoiceActive { get; set; }
	SoundFile _sample;
	SoundHandle _voice;

	internal override void OnModelLoaded( ModelLoader loader ) => EyeHeight = 28;
	internal override bool InAttackRange( GameObject target, Vector3 aim ) => false;
	internal override bool Pursue( Vector3 goal ) => MoveToward( goal, 8 );
	internal override void OnEnemySpotted( GameObject target ) => VoiceActive = true;
	internal override NpcCorpse AddCorpseComponent( GameObject go ) => go.AddComponent<StrangerCorpse>();

	internal bool Targets( PlayerController player ) => Health > 0 && LastHostile.IsValid() &&
		LastHostile.GetComponentInParent<PlayerController>() == player;

	internal override void Engage( GameObject target )
	{
		if ( target.GetComponentInParent<PlayerController>() is { } player && WorldPosition.Distance( player.WorldPosition ) <= AttackRange )
			player.GameObject.GetOrAddComponent<StrangerPresence>().Track( this );
		base.Engage( target );
	}

	internal override void ModifyDamage( Component.IDamageable.DamageEvent damage )
	{
		base.ModifyDamage( damage );
		if ( damage.DamageInfo.Tags.Has( "bullet" ) ) damage.AcceptedDamage *= 0.3f;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( !VoiceActive || Health <= 0 || Application.IsDedicatedServer ) return;
		if ( _sample is null && Sandbox.Mounting.Directory.Get( "cof" ) is GameMount mount && mount.NpcAudio.Find( VoiceSamplePath ) is { } path )
			_sample = SoundFile.Load( path );
		if ( _sample is null ) return;
		if ( !_voice.IsValid() || !_voice.IsPlaying ) _voice = GoldSrcSoundEvents.Play( _sample, WorldPosition );
		if ( _voice.IsValid() ) _voice.Position = WorldPosition;
	}

	internal override void DeathSound() =>
		EmitSound( "stranger/st_death.wav", WorldPosition, pitch: Game.Random.Int( 95, 105 ) / 100f );

	protected override void OnDeath( Component.IDamageable.DeathEvent death )
	{
		VoiceActive = false;
		_voice?.Stop();
		base.OnDeath( death );
	}

	protected override void OnDisabled()
	{
		_voice?.Stop();
		base.OnDisabled();
	}
}

[Hide]
public sealed class StrangerPresence : Component
{
	const string Heartbeat = "stranger/st_hearbeat.wav";
	readonly HashSet<Stranger> _sources = [];
	TimeUntil _nextRefresh, _nextPulse;
	bool _heartbeatPending;

	internal void Track( Stranger source ) => _sources.Add( source );

	internal static (int Alpha, float Interval) Intensity( float distance ) => distance switch
	{
		<= 75 => (192, 0.25f),
		<= 100 => (128, 0.5f),
		<= 200 => (96, 0.75f),
		<= 250 => (64, 1),
		_ => default
	};

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost || _sources.Count == 0 ) return;
		var player = GetComponent<PlayerController>();
		if ( !player.IsValid() ) return;
		_sources.RemoveWhere( source => !source.IsValid() || !source.Targets( player ) );
		var closestDistance = float.PositiveInfinity;
		Stranger closest = null;
		foreach ( var source in _sources )
		{
			var distance = source.WorldPosition.Distance( player.WorldPosition );
			if ( distance >= closestDistance ) continue;
			closestDistance = distance;
			closest = source;
		}
		var (alpha, interval) = Intensity( closestDistance );
		if ( alpha == 0 )
		{
			_heartbeatPending = false;
			return;
		}

		var heartbeat = _heartbeatPending && _nextPulse <= 0;
		if ( heartbeat ) _heartbeatPending = false;
		else if ( !_heartbeatPending && _nextPulse <= 0 )
		{
			_nextPulse = interval;
			_heartbeatPending = true;
			if ( MapState.PlayerVitals( player ) is { Health: > 0 } vitals )
				vitals.OnDamage( new DamageInfo { Damage = closest.Definition.Damage, Origin = player.WorldPosition, Position = player.EyePosition } );
		}

		if ( _nextRefresh > 0 && !heartbeat ) return;
		_nextRefresh = 0.1f;
		using ( Rpc.FilterInclude( player.Network.Owner ?? Connection.Local ) ) Show( player, alpha, heartbeat );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void Show( PlayerController player, int alpha, bool pulse )
	{
		if ( !player.IsValid() || player.IsProxy || Application.IsDedicatedServer ) return;
		if ( player.Scene.Camera is { IsValid: true } camera )
			camera.GameObject.GetOrAddComponent<ScreenFadeDisplay>().Start( Color.Black.WithAlpha( alpha / 255f ), 0.5f, 1, true );
		if ( pulse && Sandbox.Mounting.Directory.Get( "cof" ) is GameMount mount )
			NpcAudio.Play( mount.NpcAudio.Find( Heartbeat ), player.WorldPosition );
	}
}

[Hide]
public sealed class StrangerCorpse : NpcCorpse
{
	internal override bool PlayEventsOnClients => true;

	internal override void HandleEvent( int id )
	{
		if ( id is 5 or 6 ) Renderer.RenderOptions.Game = id == 6;
		else if ( id == 7 && Networking.IsHost ) GameObject.Destroy();
	}
}

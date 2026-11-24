using GoldSrc.HalfLife;
using GoldSrc.Maps;

namespace GoldSrc.ScientistSlaughterhouse;

[Hide]
public sealed class SatelliteCharge : PlacedCharge
{
	static readonly Color SignalColor = new Color32( 255, 100, 16, 255 );
	const float SkyTraceLimit = 4000;
	const float StrikeRadius = 512;
	const float StrikeDelay = 0.1f;
	const int StrikeCount = 14;
	TimeUntil _nextAction = 5;
	int _strikesLeft = StrikeCount;

	[Sync( SyncFlags.FromHost ), Change( nameof( OnUplinkChanged ) )]
	bool UplinkEstablished { get; set; }

	protected override void OnStart()
	{
		base.OnStart();
		GetComponent<SkinnedModelRenderer>().Tint = SignalColor.WithAlpha( 0.35f );
	}

	void OnUplinkChanged( bool before, bool after )
	{
		if ( after && GetComponent<SkinnedModelRenderer>() is { } renderer )
			renderer.Enabled = false;
	}

	protected override void OnFixedUpdate()
	{
		if ( !UplinkEstablished ) base.OnFixedUpdate();
		if ( !Networking.IsHost || _nextAction > 0 ) return;
		if ( !UplinkEstablished ) EstablishUplink();
		else if ( _strikesLeft == 0 ) GameObject.Destroy();
		else CallStrike();
	}

	SceneTrace Trace( Vector3 start, Vector3 end ) => Scene.Trace.Ray( start, end )
		.IgnoreGameObjectHierarchy( GameObject ).WithoutTags( "player", "playercontroller", "npc", "trigger" );

	void EstablishUplink()
	{
		var start = WorldPosition;
		var sky = Trace( start, start + Vector3.Up * SkyTraceLimit ).Run();
		ShowSignal( start + Vector3.Up * 8 );
		if ( sky.StartedSolid || (sky.Hit && sky.Surface?.Tags != GoldSrcSkySurfaceLoader.SkyTag) )
		{
			ShowBlocked( Thrower );
			GameObject.Destroy();
			return;
		}

		var body = GetComponent<Rigidbody>();
		body.MotionEnabled = false;
		GetComponent<BoxCollider>().Enabled = false;
		WorldPosition = sky.EndPosition - Vector3.Up;
		UplinkEstablished = true;
		ShowUplink( start, WorldPosition );
		_nextAction = 5;
	}

	void CallStrike()
	{
		var direction = Rotation.FromYaw( Game.Random.Float( 0, 360 ) ).Forward;
		var offset = direction * Game.Random.Float( 0, StrikeRadius );
		var sky = Trace( WorldPosition, WorldPosition + offset ).Run();
		var start = sky.Hit ? sky.EndPosition - direction : sky.EndPosition;
		var hit = Trace( start, start + Vector3.Down * (SkyTraceLimit * 2) ).Run();
		if ( hit.StartedSolid || !hit.Hit || hit.Surface?.Tags == GoldSrcSkySurfaceLoader.SkyTag )
		{
			_nextAction = 0.1f;
			return;
		}

		_strikesLeft--;
		_nextAction = _strikesLeft == 0 ? 0.2f : Game.Random.Float( 0.1f, 2 );
		ShowStrike( start, hit.EndPosition );
		Strike( hit.EndPosition, hit.Normal );
	}

	async void Strike( Vector3 position, Vector3 normal )
	{
		await GameTask.DelaySeconds( StrikeDelay );
		if ( IsValid ) Blast.Detonate( GameObject, Thrower, position, normal, 150, true, EffectsMount );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowSignal( Vector3 position )
	{
		if ( !Application.IsDedicatedServer )
			SonicRing.Spawn( position, SignalColor, 128, EffectsMount, lifetime: 1.4f );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowUplink( Vector3 start, Vector3 end )
	{
		if ( !Application.IsDedicatedServer )
			EnergyBeam.Show( start, end, SignalColor, "laserbeam", "Satellite uplink", 16, 1.4f, EffectsMount );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowStrike( Vector3 start, Vector3 end )
	{
		if ( Application.IsDedicatedServer || EffectsMount is not { } mount ) return;
		EnergyBeam.Show( start, end, Color.White, "laserbeam", "Orbital plasma beam", 35, 1, mount );
		Blast.SpriteEffect( "Orbital plasma impact", "vslash", end, 1, 15, mount: mount );
		NpcAudio.Play( mount.NpcAudio.Find( "weapons/mortarhit.wav" ), end );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void ShowBlocked( GameObject thrower )
	{
		if ( Application.IsDedicatedServer || thrower?.GetComponent<PlayerController>() is not { IsProxy: false } ) return;
		Game.ActiveScene.GetSystem<MessageDisplay>()?.Show( new TitleMessage
		{
			Text = "Target Designator Error: No sky above location!",
			Position = new Vector2( -1, 0.5f ),
			Color = Color.White,
			HoldTime = 1,
			FadeOut = 0.25f
		} );
	}
}

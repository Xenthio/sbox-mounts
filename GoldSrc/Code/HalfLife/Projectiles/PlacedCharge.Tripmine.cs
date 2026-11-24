namespace GoldSrc.HalfLife;

public partial class PlacedCharge
{
	TimeUntil _arm, _nextCheck;
	TimeUntil? _detonateAt;
	float _mineHealth = 1;
	bool _disarmed;

	[Sync( SyncFlags.FromHost )] float BeamFraction { get; set; } = -1;
	bool Armed => BeamFraction >= 0;
	LineRenderer _beam;
	GameObject _support;
	Transform _supportPose;
	SoundHandle _chargeSound;

	[Property] public float MineArmTime { get; set; } = 2.5f;
	[Property] public bool FindMineSupport { get; set; }

	internal static bool Attach( PlayerController player, SceneTraceResult hit, GameMount mount ) =>
		Attach<PlacedCharge>( player, hit, mount );

	internal static bool Attach<T>( PlayerController player, SceneTraceResult hit, GameMount mount ) where T : PlacedCharge, new()
	{
		var transform = new Transform( hit.EndPosition + hit.Normal * 8, Rotation.LookAt( hit.Normal ) );
		var charge = CreateMine<T>( transform, mount );
		if ( !charge.IsValid() ) return false;
		charge.Thrower = player.GameObject;
		charge.SetMineSupport( hit.GameObject );
		charge.GameObject.Enabled = true;
		charge.GameObject.NetworkSpawn( true, null );
		charge.PlayMineSound( false );
		return true;
	}

	internal static PlacedCharge CreateMine( Transform transform, GameMount mount ) => CreateMine<PlacedCharge>( transform, mount );

	static T CreateMine<T>( Transform transform, GameMount mount ) where T : PlacedCharge, new()
	{
		var path = mount is null ? "valve/models/v_tripmine.mdl" : mount.FindMountPath( "models/v_tripmine.mdl" );
		if ( path is null ) return null;
		var model = Model.Load( $"mount://{mount?.Ident ?? "hl1"}/{path}.vmdl" );
		if ( !model.IsValid() || model.IsError ) return null;
		var go = new GameObject( false, "Laser tripmine" ) { WorldTransform = transform };
		var renderer = go.AddComponent<SkinnedModelRenderer>();
		renderer.Model = model;
		PoseMine( renderer, "world" );
		var collider = go.AddComponent<BoxCollider>();
		collider.Scale = Vector3.One * 16;
		collider.IsTrigger = true;
		var charge = go.AddComponent<T>();
		charge.IsMine = true;
		charge.EffectsMountId = mount?.Ident;
		if ( mount is HalfLifeMModMount ) charge.MineArmTime = 1;
		return charge;
	}

	protected override void OnStart()
	{
		if ( IsMine && Networking.IsHost ) _arm = MineArmTime;
	}

	void SetMineSupport( GameObject support )
	{
		_support = support;
		if ( support.IsValid() ) _supportPose = support.WorldTransform;
	}

	GameMount MineMount => EffectsMount ?? Sandbox.Mounting.Directory.Get( "hl1" ) as GameMount;
	internal virtual bool HasTripBeam => true;
	protected bool IsMineArmed => Armed;

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void PlayMineSound( bool armed )
	{
		if ( Application.IsDedicatedServer || MineMount is not { } mount ) return;
		_chargeSound?.Stop();
		var audio = mount.NpcAudio;
		if ( armed ) NpcAudio.Play( audio.Find( "weapons/mine_activate.wav" ), WorldPosition, 0.5f, 0.75f );
		else
		{
			NpcAudio.Play( audio.Find( "weapons/mine_deploy.wav" ), WorldPosition );
			_chargeSound = NpcAudio.Play( audio.Find( "weapons/mine_charge.wav" ), WorldPosition, 0.2f, mount is HalfLifeMModMount ? 1.45f : 1 );
		}
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void StopChargeSound() => _chargeSound?.Stop();

	protected override void OnDisabled() => _chargeSound?.Stop();

	internal static void PoseMine( SkinnedModelRenderer renderer, string pose )
	{
		var body = 3;
		foreach ( var part in renderer.Model.Parts.All )
		{
			if ( part.Choices.Count == 0 ) continue;
			renderer.SetBodyGroup( part.Name, body % part.Choices.Count );
			body /= part.Choices.Count;
		}
		renderer.UseAnimGraph = false;
		renderer.Sequence.Name = pose;
	}

	void CheckMine()
	{
		if ( _detonateAt is { } delay )
		{
			if ( delay > 0 ) return;
			if ( _disarmed ) GameObject.Destroy();
			else Detonate();
			return;
		}
		if ( _nextCheck > 0 ) return;
		_nextCheck = 0.1f;
		if ( FindMineSupport && _support is null )
		{
			var forward = WorldRotation.Forward;
			var backing = Scene.Trace.Ray( WorldPosition + forward * 8, WorldPosition - forward * 32 )
				.IgnoreGameObjectHierarchy( GameObject ).Run();
			if ( backing.StartedSolid ) { _arm = (float)_arm + 0.1f; return; }
			if ( !backing.Hit ) { GameObject.Destroy(); return; }
			SetMineSupport( backing.GameObject );
		}
		if ( _support is not null && (!_support.IsValid() || _support.WorldTransform != _supportPose) )
		{
			if ( Armed ) TriggerMine();
			else
			{
				var prefab = GameObject.GetPrefab( $"mount://{EffectsMountId ?? "hl1"}/entities/items/weapon_tripmine.prefab" );
				if ( prefab.IsValid() )
					prefab.Clone( new Transform( WorldPosition + WorldRotation.Forward * 24, WorldRotation ) ).NetworkSpawn( true, null );
				GameObject.Destroy();
			}
			return;
		}
		if ( _arm > 0 ) return;
		if ( !Armed )
		{
			BeamFraction = HasTripBeam ? TraceTripBeam().Fraction : 0;
			GetComponent<BoxCollider>().IsTrigger = false;
			PlayMineSound( true );
			return;
		}
		CheckArmedMine();
	}

	SceneTraceResult TraceTripBeam() => Scene.Trace.Ray( WorldPosition, WorldPosition + WorldRotation.Forward * 2048 )
		.IgnoreGameObjectHierarchy( GameObject ).Run();

	internal virtual void CheckArmedMine()
	{
		if ( System.MathF.Abs( BeamFraction - TraceTripBeam().Fraction ) > 0.001f ) TriggerMine();
	}

	protected override void OnUpdate()
	{
		if ( !IsMine || !Armed || !HasTripBeam || _beam.IsValid() || Application.IsDedicatedServer ) return;
		var visual = new GameObject( GameObject, false, "Tripmine beam" ) { NetworkMode = NetworkMode.Never };
		_beam = visual.AddComponent<LineRenderer>();
		_beam.UseVectorPoints = true;
		var end = WorldPosition + WorldRotation.Forward * (2048 * BeamFraction);
		_beam.VectorPoints = [WorldPosition, end];
		var mount = MineMount;
		var mmod = mount is HalfLifeMModMount;
		_beam.Width = mmod ? 1.5f : 1;
		_beam.Color = new Color( 0, 214f / 255, 198f / 255, 64f / 255 );
		if ( mount is not null )
			_beam.Texturing = TrailTextureConfig.Default with { Material = mount.GetTrailMaterial( mmod ? "zbeam2" : "laserbeam" ), Scroll = 255f / 128 };
		if ( mmod )
		{
			AddMineGlow( visual, mount, "glow04", end );
			AddMineGlow( visual, mount, "glow03", WorldPosition );
		}
		_beam.Additive = true;
		_beam.Opaque = _beam.CastShadows = false;
		visual.Enabled = true;
	}

	static void AddMineGlow( GameObject parent, GameMount mount, string asset, Vector3 position )
	{
		if ( mount.GetIndexedSprite( asset ) is not { } sprite ) return;

		var glow = new GameObject( parent, false, "Tripmine glow" ) { NetworkMode = NetworkMode.Never, WorldPosition = position };
		var renderer = glow.AddComponent<SpriteRenderer>();
		var frame = sprite.Animations[0].Frames[0].Texture;
		renderer.Sprite = sprite;
		renderer.Size = new Vector2( frame.Width, frame.Height ) * 0.3f;
		renderer.Color = new Color32( 0, 214, 198, 210 );
		renderer.Additive = true;
		glow.Enabled = true;
	}
}

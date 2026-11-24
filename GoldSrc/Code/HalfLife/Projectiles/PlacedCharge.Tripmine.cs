namespace GoldSrc.HalfLife;

public sealed partial class PlacedCharge
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

	internal static bool Attach( PlayerController player, SceneTraceResult hit, GameMount mount )
	{
		var transform = new Transform( hit.EndPosition + hit.Normal * 8, Rotation.LookAt( hit.Normal ) );
		var charge = CreateMine( transform, mount );
		if ( !charge.IsValid() ) return false;
		charge.Thrower = player.GameObject;
		charge.SetMineSupport( hit.GameObject );
		charge.GameObject.Enabled = true;
		charge.GameObject.NetworkSpawn( true, null );
		charge.PlayMineSound( false );
		return true;
	}

	internal static PlacedCharge CreateMine( Transform transform, GameMount mount )
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
		var charge = go.AddComponent<PlacedCharge>();
		charge.IsMine = true;
		charge.EffectsMountId = mount?.Ident;
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
			_chargeSound = NpcAudio.Play( audio.Find( "weapons/mine_charge.wav" ), WorldPosition, 0.2f );
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
		var start = WorldPosition;
		var hit = Scene.Trace.Ray( start, start + WorldRotation.Forward * 2048 ).IgnoreGameObjectHierarchy( GameObject ).Run();
		if ( Armed )
		{
			if ( System.MathF.Abs( BeamFraction - hit.Fraction ) > 0.001f ) TriggerMine();
			return;
		}

		BeamFraction = hit.Fraction;
		GetComponent<BoxCollider>().IsTrigger = false;
		PlayMineSound( true );
	}

	protected override void OnUpdate()
	{
		if ( !IsMine || !Armed || _beam.IsValid() || Application.IsDedicatedServer ) return;
		var visual = new GameObject( GameObject, false, "Tripmine beam" ) { NetworkMode = NetworkMode.Never };
		_beam = visual.AddComponent<LineRenderer>();
		_beam.UseVectorPoints = true;
		_beam.VectorPoints = [WorldPosition, WorldPosition + WorldRotation.Forward * (2048 * BeamFraction)];
		_beam.Width = 1;
		_beam.Color = new Color( 0, 214f / 255, 198f / 255, 64f / 255 );
		if ( MineMount is { } mount )
			_beam.Texturing = TrailTextureConfig.Default with { Material = mount.GetTrailMaterial( "laserbeam" ), Scroll = 255f / 128 };
		_beam.Additive = true;
		_beam.Opaque = _beam.CastShadows = false;
		visual.Enabled = true;
	}
}

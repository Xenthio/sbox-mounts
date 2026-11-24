using GoldSrc.HalfLife;

namespace GoldSrc.SvenCoop;

[Title( "Snark Nest" ), Category( "Sven Co-op" )]
[NpcDefinition( "monster_sqknest", "w_sqknest", MonsterClass.AlienWeapon, 50, 12, 16, 100,
	Mount = "svencoop", Attack = NpcAttack.Melee, Range = 128, IdleSequence = "idle", Blood = BloodColor.Yellow )]
public sealed class SnarkNest : Npc
{
	[Property, Sync( SyncFlags.FromHost )] public bool Friendly { get; set; }
	[Sync( SyncFlags.FromHost ), Change( nameof( OnRuptured ) )] bool Ruptured { get; set; }
	TimeUntil _release;
	int _remaining;

	internal override float ViewDot => -1;
	internal override MonsterClass Classification => Friendly ? MonsterClass.PlayerAlly : base.Classification;
	internal override string[] ExtraActions => ["Walk"];

	internal override void ApplyMapProperties( Bsp.Entity entity ) =>
		Friendly = entity.GetInt( "is_player_ally" ) != 0 || entity.GetInt( "classify" ) == (int)MonsterClass.PlayerAlly;

	internal override string MapModel( Bsp.Entity entity ) => entity.GetString( "new_model" );
	internal override StudioSequence MovementSequence( float speed ) => speed > 12 ? Find( "Walk" ) : null;
	internal override void OnModelLoaded( ModelLoader loader ) => EyeHeight = 20;
	internal override bool Pursue( Vector3 goal ) => MoveToward( goal, 64 );

	internal override void PlaySequenceSound( NpcAudio audio, int id )
	{
		if ( id != 1 ) return;
		var sample = Game.Random.Int( 1, 6 );
		if ( sample >= 4 ) sample++;
		NpcAudio.Play( audio.Find( $"debris/flesh{sample}.wav" ), WorldPosition,
			Game.Random.Float( 0.5f, 0.75f ), pitch: Game.Random.Float( 0.9f, 1.1f ), attenuation: 0.8f );
	}

	internal override void BeginAttack( GameObject target )
	{
		if ( Ruptured ) return;
		Movement?.Stop();
		BeginRupture();
		_remaining = 5;
		_release = 0.05f;
	}

	protected override void Think()
	{
		if ( !Ruptured ) base.Think();
	}

	internal override void ModifyDamage( Component.IDamageable.DamageEvent hit )
	{
		if ( Ruptured ) hit.Cancelled = true;
		else base.ModifyDamage( hit );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( !Networking.IsHost || Health <= 0 || !Ruptured || _release > 0 ) return;

		ReleaseSnark();
		if ( --_remaining > 0 ) _release = 0.1f;
		else Health = 0;
	}

	protected override void OnDeath( Component.IDamageable.DeathEvent death )
	{
		if ( !Ruptured )
		{
			BeginRupture();
			ReleaseSnark();
		}
		base.OnDeath( death );
	}

	void BeginRupture()
	{
		Ruptured = true;
		ApplyRupture();
		EmitSound( $"debris/bustflesh{Game.Random.Int( 1, 2 )}.wav", WorldPosition,
			pitch: Game.Random.Float( 0.9f, 1.1f ), attenuation: 0.8f );
		var floor = Scene.Trace.Ray( WorldPosition + Vector3.Up, WorldPosition + Vector3.Down * 100 )
			.IgnoreGameObjectHierarchy( GameObject ).WithoutTags( "npc", "player", "trigger" ).Run();
		ShowRupture( WorldPosition, floor.Hit ? floor.EndPosition : null, floor.Normal );
	}

	void OnRuptured( bool before, bool after )
	{
		if ( after ) ApplyRupture();
	}

	void ApplyRupture()
	{
		if ( Renderer.IsValid() ) Renderer.Enabled = false;
		if ( GetComponentInChildren<ModelHitboxes>( true ) is { } hitboxes ) hitboxes.Enabled = false;
		if ( GetComponentInChildren<BoxCollider>( true ) is { } hull ) hull.Enabled = false;
	}

	void ReleaseSnark()
	{
		var prefab = GameObject.GetPrefab( NestSnark.Prefab );
		if ( !prefab.IsValid() ) return;
		var velocity = new Vector3( Game.Random.Float( -100, 100 ), Game.Random.Float( -100, 100 ), Game.Random.Float( 100, 200 ) );
		var go = prefab.Clone( new Transform( WorldPosition + Vector3.Up, Rotation.LookAt( velocity.WithZ( 0 ) ) ), startEnabled: false );
		var snark = go.GetComponent<NestSnark>( true );
		snark.Friendly = Friendly;
		if ( GameObject.Tags.Has( Maps.MonsterClip.MonsterTag ) ) Maps.MonsterClip.Restrict( go );
		go.Enabled = true;
		snark.Launch( velocity );
		go.NetworkSpawn( true, null );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static async void ShowRupture( Vector3 position, Vector3? floor, Vector3 normal )
	{
		if ( Application.IsDedicatedServer || Sandbox.Mounting.Directory.Get( "svencoop" ) is not GameMount mount ) return;
		if ( floor is { } point )
			ImpactDecal.Place( point, normal, $"yblood{Game.Random.Int( 1, 6 )}", 0, mount );

		var scene = Game.ActiveScene;
		var model = await Model.LoadAsync( "mount://svencoop/svencoop/models/fleshgibs.mdl.vmdl" );
		if ( !scene.IsValid() || scene != Game.ActiveScene || !model.IsValid() || model.IsError ||
			model.Parts.All.FirstOrDefault() is not { } part ) return;
		for ( var i = 0; i < 20; i++ )
		{
			var offset = new Vector3( Game.Random.Float( -8, 8 ), Game.Random.Float( -4, 4 ), Game.Random.Float( 0, 16 ) );
			var velocity = new Vector3( 50, 50, 50 ) + Vector3.Random * 10;
			HalfLife.Gib.Spawn( model, part.Name, Game.Random.Int( 0, part.Choices.Count - 1 ), position + offset, velocity, BloodColor.Yellow );
		}
	}

	protected override GameObject CreateCorpse() => null;
}

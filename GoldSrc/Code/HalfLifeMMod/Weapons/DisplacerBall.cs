using GoldSrc.HalfLife;
using Sandbox.Npcs;

namespace GoldSrc.HalfLifeMMod;

[Hide]
public sealed class DisplacerBall : NpcProjectile
{
	const float ZapRange = 512;
	const float DischargeRadius = 256;

	TimeUntil _nextArc;
	TimeUntil _nextZap;
	SoundHandle _crawl;

	internal override float LaunchSpeed => 500;
	internal override float Lifetime => 12;

	protected override void OnStart()
	{
		if ( Application.IsDedicatedServer || ModelMount is not { } mount ) return;
		AddGlow( mount, "displacer_ball", 0.5f, Color.White );
		AddGlow( mount, "displacer_animglow", 0.37f, new Color32( 255, 255, 170, 200 ) );
		ProjectileTrail.Attach( GameObject, mount, "Displacer trail", "displacer_trail", 0.2f, 6, new Color32( 255, 255, 255, 164 ) );
		_crawl = PlaySample( "weapons/displacer_crawl.wav", WorldPosition );
	}

	void AddGlow( GameMount mount, string name, float scale, Color color )
	{
		if ( mount.GetIndexedSprite( name ) is not { } sprite ) return;
		var image = sprite.Animations[0].Frames[0].Texture;
		var renderer = new GameObject( GameObject, true, name ).AddComponent<SpriteRenderer>();
		renderer.GameObject.NetworkMode = NetworkMode.Never;
		renderer.Sprite = sprite;
		renderer.Size = new Vector2( image.Width, image.Height ) * scale;
		renderer.Color = color;
		renderer.Additive = true;
	}

	protected override void OnUpdate()
	{
		if ( _crawl.IsValid() ) _crawl.Position = WorldPosition;
	}

	protected override void OnDestroy() => _crawl?.Stop();

	internal override SceneTrace FlightTrace( Vector3 start, Vector3 end ) =>
		Scene.Trace.Box( BBox.FromPositionAndSize( Vector3.Zero, Vector3.One * 8 ), start, end )
			.WithoutTags( "trigger", "playercontroller" );

	internal override void Guide()
	{
		if ( _nextArc > 0 ) return;
		_nextArc = 0.1f;

		var distance = 1024f;
		Vector3? surface = null;
		for ( var i = 0; i < 10; ++i )
		{
			var trace = TraceArc( WorldPosition + Vector3.Random.Normal * distance ).WithoutTags( "npc", "player" ).Run();
			if ( !trace.Hit || trace.StartedSolid ) continue;
			surface = trace.EndPosition;
			distance = WorldPosition.Distance( trace.EndPosition );
		}

		if ( surface is { } point ) ShowArc( WorldPosition, point, false );
		if ( _nextZap > 0 ) return;
		TryZap();
	}

	SceneTrace TraceArc( Vector3 end )
	{
		var trace = Scene.Trace.Ray( WorldPosition, end ).IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "trigger", "playercontroller" );
		return Shooter.IsValid() ? trace.IgnoreGameObjectHierarchy( Shooter ) : trace;
	}

	void TryZap()
	{
		var candidates = Scene.FindInPhysics( new Sphere( WorldPosition, ZapRange ) )
			.Select( obj => obj.GetComponentInParent<IDamageable>() as Component )
			.Where( victim => victim.IsValid() && victim.GameObject.Root != Shooter?.Root && BaseNpc.IsLiveTarget( victim.GameObject ) )
			.Distinct().OrderBy( victim => WorldPosition.DistanceSquared( victim.WorldPosition ) );

		foreach ( var victim in candidates )
		{
			var point = Npc.TargetBodyPosition( victim.GameObject );
			if ( WorldPosition.DistanceSquared( point ) >= ZapRange * ZapRange ) continue;
			var hit = TraceArc( point ).UseHitboxes().Run();
			if ( hit.Hit && hit.GameObject?.Root != victim.GameObject.Root ) continue;

			var damage = new DamageInfo { Damage = 20, Attacker = Shooter, Weapon = GameObject, Origin = WorldPosition, Position = point, Hitbox = hit.Hitbox };
			damage.Tags.Add( "energybeam" );
			((IDamageable)victim).OnDamage( damage );
			_nextZap = 0.5f;
			ShowArc( WorldPosition, point, true );
			return;
		}
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowArc( Vector3 start, Vector3 end, bool zap )
	{
		if ( Application.IsDedicatedServer ) return;
		EnergyBeam.Show( start, end, new Color32( 149, 165, 50, 200 ), "lgtning", "Displacer arc", zap ? 2.5f : 2, 0.12f, ModelMount );
		if ( zap ) PlaySample( $"weapons/displacer_zap{Game.Random.Int( 1, 3 )}.wav", start );
	}

	internal override void HandleContact( SceneTraceResult hit )
	{
		var point = hit.EndPosition + hit.Normal;
		Blast.DamageInRadius( GameObject, Shooter, point, Damage, DischargeRadius, "energybeam", "blast", "alwaysgib" );
		GroundShake.Emit( Scene, point, 12, 100, 1, 350 );
		ShowDischarge( point );
		GameObject.Destroy();
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowDischarge( Vector3 point )
	{
		if ( Application.IsDedicatedServer || ModelMount is not { } mount ) return;
		var sound = Game.Random.Int( 0, 1 ) == 0 ? "displacer_teleport" : "displacer_teleport2";
		PlaySample( $"weapons/{sound}.wav", point );
		SonicRing.Spawn( point, Color.White, DischargeRadius, mount, 10, "displacer_shock", 0.3f );
		Blast.SpriteEffect( "Displacer burst", "displacer_fireball", point, 1, 15, mount: mount );
	}

	SoundHandle PlaySample( string sample, Vector3 position ) => ModelMount is { } mount
		? NpcAudio.Play( mount.NpcAudio.Find( sample ), position )
		: null;
}

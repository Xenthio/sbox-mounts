using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

[Hide]
public sealed class DisplacerBall : NpcProjectile
{
	TimeUntil _nextArc, _detonate;
	bool _impacted;

	internal override float Lifetime => 4;
	internal override string HitTag => "energybeam";

	protected override void OnStart()
	{
		if ( Application.IsDedicatedServer || ModelMount is not { } mount ) return;
		if ( mount.GetIndexedSprite( "exit1" ) is { } sprite )
		{
			var renderer = GameObject.AddComponent<SpriteRenderer>();
			var frame = sprite.Animations[0].Frames[0].Texture;
			renderer.Sprite = sprite;
			renderer.Size = new Vector2( frame.Width, frame.Height );
			renderer.Additive = true;
		}
		PlayEffectSound( "weapons/displacer_start.wav", WorldPosition );
	}

	internal override SceneTrace FlightTrace( Vector3 start, Vector3 end ) =>
		Scene.Trace.Box( BBox.FromPositionAndSize( Vector3.Zero, Vector3.One * 8 ), start, end ).WithoutTags( "trigger" );

	internal override void Guide()
	{
		if ( _nextArc > 0 ) return;
		_nextArc = 0.1f;
		var basis = WorldRotation;
		Arc( basis.Left );
		Arc( basis.Right );
	}

	void Arc( Vector3 direction )
	{
		var end = WorldPosition + (direction + Vector3.Random * 0.75f).Normal * 1024;
		var trace = Scene.Trace.Ray( WorldPosition, end ).IgnoreGameObjectHierarchy( GameObject );
		if ( Shooter.IsValid() ) trace = trace.IgnoreGameObjectHierarchy( Shooter );
		var hit = trace.WithoutTags( "npc", "player", "trigger" ).Run();
		if ( hit.Hit && !hit.StartedSolid ) ShowArc( WorldPosition, hit.EndPosition );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowArc( Vector3 start, Vector3 end ) =>
		EnergyBeam.Show( start, end, new Color32( 100, 192, 255, 64 ), "lgtning", "Displacer arc", 4.8f, 0.12f, ModelMount );

	internal override void HandleContact( SceneTraceResult hit )
	{
		if ( hit.GameObject?.GetComponentInParent<IDamageable>() is { } victim )
			DamageVictim( victim, hit.EndPosition, hit.Hitbox );
		WorldPosition = hit.EndPosition;
		Velocity = Vector3.Zero;
		_impacted = true;
		_detonate = 0.6f;
		ShowImpact( WorldPosition );
	}

	protected override void OnFixedUpdate()
	{
		if ( !_impacted ) { base.OnFixedUpdate(); return; }
		if ( !Networking.IsHost || _detonate > 0 ) return;
		Blast.DamageInRadius( GameObject, Shooter, WorldPosition, Damage, 300, "energybeam", "alwaysgib" );
		ShowDischarge( WorldPosition );
		GameObject.Destroy();
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowImpact( Vector3 position )
	{
		PlayEffectSound( "weapons/displacer_impact.wav", position );
		SonicRing.Spawn( position, Color.White, 750, ModelMount, width: 2.5f, texture: "disp_ring", lifetime: 0.4f );
		Blast.SpriteEffect( "Displacer impact flare", "b-tele1", position, 1, 10, mount: ModelMount );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowDischarge( Vector3 position ) => PlayEffectSound( "weapons/displacer_teleport.wav", position );

	void PlayEffectSound( string sample, Vector3 position )
	{
		if ( !Application.IsDedicatedServer && ModelMount is { } mount )
			NpcAudio.Play( mount.NpcAudio.Find( sample ), position );
	}
}

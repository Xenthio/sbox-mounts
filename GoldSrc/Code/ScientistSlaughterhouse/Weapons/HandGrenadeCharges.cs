using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

internal enum GrenadePayload { Explosive, Incendiary, Emp, ClusterBomblet, ClusterShell }

[Hide]
public sealed class SlaughterhouseGrenade : TimedGrenade
{
	[Sync( SyncFlags.FromHost )] internal GrenadePayload Payload { get; set; }
	internal override bool Solid => Payload != GrenadePayload.ClusterShell;

	protected override void OnStart()
	{
		if ( Payload == GrenadePayload.ClusterBomblet && !Application.IsDedicatedServer &&
			Sandbox.Mounting.Directory.Get( "ssh" ) is GameMount mount )
			ProjectileTrail.Attach( GameObject, mount, "Cluster bomblet trail", "smoke", 1.5f, 3, new Color32( 224, 224, 255, 255 ) );
	}

	internal override void Detonate()
	{
		switch ( Payload )
		{
			case GrenadePayload.Incendiary:
				var hit = TraceExplosionSurface();
				IncendiaryBlast.Detonate( GameObject, Thrower, hit.Hit ? hit.EndPosition : WorldPosition,
					hit.Normal, 100, EffectsMount, plasma: false );
				break;
			case GrenadePayload.Emp:
				LaserGrenadeField.Create( Thrower, WorldPosition, 100, EffectsMount, emp: true, fullDamageTime: 0, "EMP grenade field" );
				break;
			default:
				Explode( Payload == GrenadePayload.ClusterShell ? 25 : 100 );
				break;
		}
	}
}

[Hide]
public sealed class ClusterGrenade : TimedGrenade
{
	internal bool Shells { get; set; }

	internal override void Detonate()
	{
		var origin = Explode();
		var model = Shells ? "shotgunshell" : "bomblet";
		if ( EffectsMount is not { } mount || mount.FindMountPath( $"models/{model}.mdl" ) is not { } path ) return;
		var modelPath = $"mount://{mount.Ident}/{path}.vmdl";
		var siblings = new List<SlaughterhouseGrenade>( 6 );
		for ( var i = 0; i < (Shells ? 40 : 6); i++ )
		{
			var spread = Shells ? 0.25f : 1;
			var direction = new Vector3( Game.Random.Float( -spread, spread ), Game.Random.Float( -spread, spread ),
				Shells ? 1.5f : Game.Random.Float( 0.5f, 1 ) );
			var speed = Shells ? Game.Random.Float( 300, 400 ) : Game.Random.Float( 150, 300 );
			var child = Spawn<SlaughterhouseGrenade>( Thrower, origin + direction * 8, direction * speed,
				Shells ? 0.5f : Game.Random.Float( 2.5f, 3.5f ), Shells ? "Cluster shell" : "Cluster bomblet", modelPath, charge =>
				{
					charge.Payload = Shells ? GrenadePayload.ClusterShell : GrenadePayload.ClusterBomblet;
					foreach ( var sibling in siblings ) charge.AddComponent<PhysicsFilter>().Body = sibling.GameObject;
				}, mount );
			if ( !Shells ) siblings.Add( child );
		}
	}
}

[Hide]
public sealed class ShockwaveGrenade : TimedGrenade
{
	const float Radius = 192;

	internal override void Detonate()
	{
		var origin = WorldPosition;
		var targets = Scene.FindInPhysics( new Sphere( origin, Radius ) ).Select( RepulsorGun.GetMovable ).Distinct().ToArray();
		foreach ( var target in targets )
		{
			if ( !target.IsValid() || target == GameObject ) continue;
			var position = Npc.TargetHullCenter( target );
			var offset = position - origin;
			if ( offset.LengthSquared > Radius * Radius ) continue;
			var hit = Scene.Trace.Ray( origin, position ).IgnoreGameObjectHierarchy( GameObject )
				.IgnoreGameObjectHierarchy( target ).WithoutTags( "npc", "player", "playercontroller", "trigger" ).Run();
			if ( hit.Hit ) continue;
			var current = target.GetComponent<PlayerController>()?.Body?.Velocity ?? target.GetComponent<Rigidbody>()?.Velocity ?? Vector3.Zero;
			RepulsorGun.GiveVelocity( target, current + offset.Normal * 1000 + Vector3.Up * 200 );
		}
		EffectsMount?.NpcAudio.Emit( "weapons/concgren_blast1.wav", origin );
		ShowWave( origin );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowWave( Vector3 origin )
	{
		if ( !Application.IsDedicatedServer && Sandbox.Mounting.Directory.Get( "ssh" ) is GameMount mount )
			SonicRing.Spawn( origin, new Color32( 0, 128, 255, 32 ), 768, mount, width: 64 );
	}
}

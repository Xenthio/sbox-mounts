using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

public sealed partial class ReflectionLaser
{
	static readonly Vector3[] BeamOffsets = [Vector3.Zero, Vector3.Left * 30, Vector3.Right * 30, Vector3.Up * 30, Vector3.Down * 30];
	static readonly TagSet ReflectedDamage = ["bullet"];
	static readonly TagSet CuttingDamage = ["energybeam"];

	void TraceBeams( Ray aim, bool cutting, bool mark, List<Vector3> segments )
	{
		if ( cutting )
		{
			TraceCuttingBeam( aim, mark, segments );
			return;
		}

		foreach ( var offset in BeamOffsets )
		{
			var start = aim.Position + offset;
			var direction = aim.Forward;
			for ( int bounce = 0; bounce < 3; ++bounce )
			{
				var hit = TraceSegment( start, direction, Owner.GameObject );
				if ( hit.StartedSolid ) break;
				segments?.Add( bounce == 0 ? aim.Position : start );
				segments?.Add( hit.EndPosition );
				if ( !hit.Hit ) break;

				if ( hit.GameObject?.GetComponentInParent<IDamageable>() is not null )
					ShootBullet( hit, 2, 0, ReflectedDamage );
				if ( !IsBrush( hit ) ) break;
				if ( mark ) ShowMark( hit.EndPosition, hit.Normal, false );

				direction = Vector3.Reflect( direction, hit.Normal );
				start = hit.EndPosition + hit.Normal * 0.1f;
			}
		}
	}

	void TraceCuttingBeam( Ray aim, bool mark, List<Vector3> segments )
	{
		var start = aim.Position;
		var ignore = Owner.GameObject;
		var end = start + aim.Forward * BeamRange;
		for ( int pass = 0; pass < 5; ++pass )
		{
			var hit = TraceSegment( start, aim.Forward, ignore );
			if ( hit.StartedSolid ) break;
			end = hit.EndPosition;
			if ( !hit.Hit ) break;
			if ( hit.GameObject?.GetComponentInParent<IDamageable>() is not null )
			{
				ShootBullet( hit, 10, 0, CuttingDamage );
				break;
			}

			if ( !IsBrush( hit ) )
			{
				ignore = hit.GameObject;
				start = end + aim.Forward * 0.1f;
				continue;
			}

			if ( mark ) ShowMark( end, hit.Normal, false );
			var exit = TraceLineOfFire( new Ray( end + aim.Forward * 8, -aim.Forward ), 8.1f, 0 )
				.RunAll().FirstOrDefault( trace => trace.Collider == hit.Collider && trace.GameObject == hit.GameObject );
			if ( !exit.Hit || exit.StartedSolid ) break;
			if ( mark ) ShowMark( exit.EndPosition, exit.Normal, true );
			start = exit.EndPosition + aim.Forward * 0.1f;
			ignore = Owner.GameObject;
		}
		segments?.Add( aim.Position );
		segments?.Add( end );
	}

	SceneTraceResult TraceSegment( Vector3 start, Vector3 direction, GameObject ignore )
	{
		var trace = TraceLineOfFire( new Ray( start, direction ), BeamRange, 0 ).WithoutTags( "trigger" ).UseHitboxes();
		if ( ignore.IsValid() ) trace = trace.IgnoreGameObjectHierarchy( ignore );
		return trace.Run();
	}

	static bool IsBrush( SceneTraceResult hit ) =>
		hit.GameObject?.GetComponentInParent<IDamageable>() is null && hit.Body?.BodyType != PhysicsBodyType.Dynamic;

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowMark( Vector3 position, Vector3 normal, bool exit ) =>
		ImpactDecal.Place( position, normal, $"{(exit ? "bigshot" : "shot")}{Game.Random.Int( 1, 5 )}", 10, ContentMount );
}

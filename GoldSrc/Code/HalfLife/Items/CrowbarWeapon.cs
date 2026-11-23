namespace GoldSrc.HalfLife;

[Hide]
public abstract class CrowbarWeapon : StudioWeapon
{
	static readonly BBox HeadHull = new( new Vector3( -16, -16, -18 ), new Vector3( 16, 16, 18 ) );

	public override Ray AimRay => Holder switch
	{
		PlayerController player => player.EyeTransform.ForwardRay,
		{ } holder => holder.CalculateAimRay( this ),
		null => UnheldAimRay
	};

	protected SceneTraceResult TraceSwing( Ray aim ) => TraceMelee( aim, Ballistics.Range );

	protected SceneTraceResult TraceMelee( Ray aim, float range )
	{
		var trace = BulletTrace( aim, range, 0 );
		var line = trace.Run();
		if ( line.Hit ) return line;
		var hull = trace.Size( HeadHull ).Run();
		if ( !hull.Hit || hull.GameObject?.GetComponentInParent<IDamageable>() is not null ) return hull;

		// FindHullIntersection resolves a hull hit to a point on the actual surface.
		var end = aim.Position + (hull.EndPosition - aim.Position) * 2;
		line = trace.FromTo( aim.Position, end ).Run();
		if ( line.Hit ) return line;
		var nearest = float.MaxValue;
		foreach ( var corner in HeadHull.Corners )
		{
			line = trace.FromTo( aim.Position, end + corner ).Run();
			if ( !line.Hit || line.Distance >= nearest ) continue;
			nearest = line.Distance;
			hull = line;
		}
		return hull;
	}
}

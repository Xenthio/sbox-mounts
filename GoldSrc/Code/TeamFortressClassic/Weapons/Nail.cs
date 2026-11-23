using GoldSrc.HalfLife;

namespace GoldSrc.TeamFortressClassic;

[Hide]
public class Nail : NpcProjectile
{
	internal override float Lifetime => 6;
	internal override string HitTag => "bullet";
	internal override SceneTrace FlightTrace( Vector3 start, Vector3 end ) =>
		base.FlightTrace( start, end ).WithoutTags( "trigger", "playerclip" );
}

[Hide]
public sealed class RailSlug : Nail
{
	internal override string HitTag => "energybeam";

	protected override void OnStart()
	{
		if ( Application.IsDedicatedServer ) return;
		ProjectileTrail.Attach( GameObject, null, "Railgun tracer", null, 0.05f, 0.6f, new Color( 0.2f, 1, 0.25f ) );
	}
}

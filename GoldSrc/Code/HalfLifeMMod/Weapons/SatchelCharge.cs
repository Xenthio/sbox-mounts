using GoldSrc.HalfLife;

namespace GoldSrc.HalfLifeMMod;

[Hide]
public sealed class SatchelCharge : PlacedCharge
{
	internal override void OnSatchelContact( Rigidbody body, Collision collision )
	{
		var floor = Scene.Trace.Ray( WorldPosition, WorldPosition + Vector3.Down * 10 )
			.IgnoreGameObjectHierarchy( GameObject ).WithoutTags( "npc", "player", "playercontroller", "trigger" ).Run();
		var supported = floor.Hit && floor.Normal.z > 0.7f;
		if ( supported )
		{
			body.Velocity *= 0.95f;
			body.AngularVelocity *= 0.9f;
		}

		if ( !supported || System.MathF.Abs( collision.Contact.Normal.z ) <= 0.7f )
			base.OnSatchelContact( body, collision );
	}
}

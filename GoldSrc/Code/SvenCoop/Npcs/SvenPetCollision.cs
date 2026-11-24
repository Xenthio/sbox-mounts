using Sandbox.Physics;

namespace GoldSrc.SvenCoop;

internal static class SvenPetCollision
{
	const string PetTag = "svencoop_player_pet";

	internal static void SetIgnoresPlayers( GameObject pet, bool shouldIgnore )
	{
		if ( !shouldIgnore )
		{
			pet.Tags.Remove( PetTag );
			return;
		}
		Ignore( pet.Scene, PetTag, "player", "playercontroller" );
		pet.Tags.Add( PetTag );
	}

	internal static bool IsNotPlayer( Component actor ) => !actor.GetComponentInParent<PlayerController>().IsValid();

	internal static void Ignore( Scene scene, string tag, params string[] otherTags )
	{
		var world = scene.PhysicsWorld;
		var existing = world.CollisionRules;
		var missing = otherTags.Where( other => existing?.Pairs.ContainsKey( (tag, other) ) != true &&
			existing?.Pairs.ContainsKey( (other, tag) ) != true ).ToArray();
		if ( missing.Length == 0 ) return;

		// Extend this scene's rules without changing those shared by other worlds.
		var rules = new CollisionRules();
		if ( existing is not null )
		{
			rules.Defaults = new( existing.Defaults );
			rules.Pairs = new( existing.Pairs );
		}
		foreach ( var other in missing ) rules.Pairs[(tag, other)] = CollisionRules.Result.Ignore;
		world.CollisionRules = Json.Deserialize<CollisionRules>( Json.Serialize( rules ) );
	}
}

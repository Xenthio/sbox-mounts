using GoldSrc.HalfLife;
using Sandbox.Npcs;

namespace GoldSrc.ScientistSlaughterhouse;

internal static class ProjectileTarget
{
	internal static GameObject Find( Scene scene, GameObject shooter, Ray ray, IReadOnlyCollection<GameObject> recentHits = null,
		float range = 1024, float minimumAlignment = 0.9f )
	{
		GameObject best = null;
		var alignment = minimumAlignment;
		foreach ( var obj in scene.FindInPhysics( new Sphere( ray.Position, range ) ) )
		{
			if ( obj.GetComponentInParent<Component.IDamageable>() is not Component victim ) continue;
			var candidate = victim.GameObject;
			if ( candidate == shooter || recentHits?.Contains( candidate ) == true || !BaseNpc.IsLiveTarget( candidate ) ) continue;
			if ( Npc.ClassRelationship( MonsterClass.PlayerWeapon, Npc.ClassOf( candidate ) ).Disposition != Disposition.Hostile ) continue;
			var point = Npc.TargetBodyPosition( candidate );
			var dot = ray.Forward.Dot( (point - ray.Position).Normal );
			if ( dot <= alignment ) continue;
			var hit = scene.Trace.Ray( ray.Position, point ).IgnoreGameObjectHierarchy( shooter ).WithoutTags( "trigger" ).Run();
			if ( hit.Hit && hit.GameObject?.Root != candidate.Root ) continue;
			best = candidate;
			alignment = dot;
		}
		return best;
	}
}

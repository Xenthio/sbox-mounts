using GoldSrc.HalfLife;
using Sandbox.Npcs;

namespace GoldSrc.SvenCoop;

[Hide]
public sealed class RevivableCorpse : Component
{
	internal Npc Actor { get; set; }

	internal bool CanRevive( PlayerController medic ) => Actor.IsValid() && !Actor.GameObject.Enabled &&
		Actor.GetRelationship( medic.GameObject ).Disposition is not (Disposition.Hostile or Disposition.Fearful);

	internal bool TryFindStandingPlace( out Transform placement )
	{
		placement = default;
		if ( !Actor.IsValid() ) return false;
		var radius = Actor.Definition.Radius;
		var hull = new BBox( new Vector3( -radius, -radius, 0 ), new Vector3( radius, radius, Actor.Definition.Height ) );
		var origin = WorldPosition - Vector3.Up * Actor.ModelOriginHeight;
		// Leave clearance for the standing hull on the corpse's support surface.
		var start = origin + Vector3.Up * 4;
		var trace = Scene.Trace.Box( hull, start, start + Vector3.Up * 0.1f )
			.IgnoreGameObjectHierarchy( GameObject ).IgnoreGameObjectHierarchy( Actor.GameObject )
			.WithoutTags( "trigger", "debris", "ragdoll", "nocollide_props" ).Run();
		if ( trace.StartedSolid || trace.Hit ) return false;
		placement = Actor.WorldTransform.WithPosition( start );
		return true;
	}

	internal void Revive( Transform placement )
	{
		var npc = Actor;
		Actor = null;
		npc.RestoreFromCorpse( placement );
		GameObject.Destroy();
	}

	protected override void OnUpdate()
	{
		if ( Networking.IsHost && !Actor.IsValid() ) Destroy();
	}

	protected override void OnDestroy()
	{
		if ( Networking.IsHost && Actor.IsValid() && !Actor.GameObject.Enabled ) Actor.GameObject.Destroy();
	}
}

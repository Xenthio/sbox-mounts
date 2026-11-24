using System;
using GoldSrc.Maps;

namespace GoldSrc.SvenCoop;

// Sandbox owns player ragdolls and observers. Use its public respawn entry point when present.
internal static class SandboxPlayerRevival
{
	static TypeDescription CorpseType => Game.TypeLibrary.GetType( "DeathCameraTarget" );
	static Connection CorpseOwner( Component corpse ) =>
		CorpseType?.GetProperty( "Connection" )?.GetValue( corpse ) as Connection;

	static Component LatestCorpse( Scene scene, Connection owner )
	{
		if ( CorpseType is not { } type || type.GetProperty( "Created" ) is not { } created ) return null;
		return scene.GetAllComponents( type.TargetType ).Where( corpse => CorpseOwner( corpse ) == owner )
			.OrderByDescending( corpse => (DateTime)created.GetValue( corpse ) ).FirstOrDefault();
	}

	internal static Component Find( GameObject hit )
	{
		if ( !hit.IsValid() || CorpseType is not { } type ) return null;
		var corpse = hit.Components.Get( type.TargetType, FindMode.Enabled | FindMode.InSelf | FindMode.InAncestors );
		if ( !corpse.IsValid() || CorpseOwner( corpse ) is not { } owner || !Connection.All.Contains( owner ) ) return null;
		if ( corpse.Scene.GetAllComponents<PlayerController>().Any( player => player.Network.Owner == owner ) ) return null;
		return LatestCorpse( corpse.Scene, owner ) == corpse ? corpse : null;
	}

	internal static bool TryRevive( Component corpse )
	{
		if ( !Networking.IsHost || !corpse.IsValid() || Find( corpse.GameObject ) != corpse ) return false;
		var scene = corpse.Scene;
		var owner = CorpseOwner( corpse );
		var center = corpse.WorldPosition;
		if ( corpse.GetComponent<SkinnedModelRenderer>() is { } renderer && renderer.TryGetBoneTransform( "pelvis", out var pelvis ) )
			center = pelvis.Position;

		var ground = scene.Trace.Ray( center + Vector3.Up * 20, center + Vector3.Down * 72 )
			.WithoutTags( "ragdoll", "trigger", "debris", "nocollide_props", "player", "playercontroller", "npc" ).Run();
		if ( !ground.Hit || ground.StartedSolid || ground.Normal.z < 0.7f ) return false;
		var position = ground.EndPosition + Vector3.Up * 4;
		var hull = new BBox( new Vector3( -16, -16, 0 ), new Vector3( 16, 16, 72 ) );
		var standing = scene.Trace.Box( hull, position, position + Vector3.Up * 0.1f )
			.WithoutTags( "ragdoll", "trigger", "debris", "nocollide_props" ).Run();
		if ( standing.StartedSolid || standing.Hit ) return false;

		var managerType = Game.TypeLibrary.GetType( "GameManager" );
		if ( managerType?.GetMethod( "Respawn" ) is not { IsPublic: true } respawn ) return false;
		GameObjectSystem manager = null;
		scene.RunEvent<Component.INetworkListener>( listener =>
		{
			if ( listener is GameObjectSystem system && system.GetType() == managerType.TargetType ) manager = system;
		} );
		if ( manager is null || respawn.InvokeWithReturn<Component>( manager, [owner] ) is not { } spawned ) return false;
		if ( spawned.GetComponent<PlayerController>() is not { } player ) return false;

		if ( MapState.PlayerVitals( player ) is { } vitals ) vitals.Health = vitals.MaxHealth * 0.5f;
		FinishRevival( player, owner, position, corpse.WorldRotation.Angles().yaw );
		return true;
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	static void FinishRevival( PlayerController player, Connection owner, Vector3 position, float yaw )
	{
		if ( player.IsValid() && (Networking.IsHost || !player.IsProxy) )
		{
			player.WorldPosition = position;
			player.EyeAngles = new Angles( 0, yaw, 0 );
			player.GameObject.Network.ClearInterpolation();
		}
		LatestCorpse( Game.ActiveScene, owner )?.GameObject.Destroy();
	}
}

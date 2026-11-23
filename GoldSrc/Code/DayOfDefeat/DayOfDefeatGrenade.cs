using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Hide]
public sealed class DayOfDefeatGrenade : TimedGrenade, Component.IPressable
{
	const float PickupReach = 96;
	const float BlastDamage = 180;
	bool _claimed;

	[Property] public string GrenadeModel { get; set; }
	[Property] public bool Underhand { get; set; }

	internal override float Friction => Underhand ? 0.6f : 0.8f;

	static GameMount ContentMount => Sandbox.Mounting.Directory.Get( "dod" ) as DayOfDefeatMount;

	internal static void Throw( GameObject thrower, Vector3 origin, Vector3 velocity, float fuse, string model, bool underhand )
	{
		if ( ContentMount?.FindFile( $"models/w_{model}.mdl" ) is not { } path ) return;
		Spawn<DayOfDefeatGrenade>( thrower, origin, velocity, fuse, "Live DoD grenade", $"mount://dod/{path}.vmdl", grenade =>
		{
			grenade.GrenadeModel = model;
			grenade.Underhand = underhand;
		} );
	}

	internal override void OnBounce( Rigidbody body ) =>
		ContentMount?.NpcAudio.Emit( "weapons/grenade_hit", WorldPosition, volume: 0.25f );

	bool IPressable.CanPress( IPressable.Event e ) => !_claimed && e.Source?.GetComponentInParent<PlayerController>() is not null;

	bool IPressable.Press( IPressable.Event e )
	{
		if ( e.Source?.GetComponentInParent<PlayerController>() is not { } player ) return false;
		PickUp( player.GameObject );
		return true;
	}

	[Rpc.Host]
	void PickUp( GameObject source )
	{
		if ( _claimed || source?.GetComponent<PlayerController>() is not { } player || FuseRemaining <= 0 ||
			player.EyePosition.Distance( WorldPosition ) > PickupReach ||
			(Rpc.Caller != Connection.Local && player.Network.Owner != Rpc.Caller) ) return;

		var reach = Scene.Trace.Ray( player.EyePosition, WorldPosition ).IgnoreGameObjectHierarchy( source )
			.IgnoreGameObjectHierarchy( GameObject ).WithoutTags( "trigger" ).Run();
		if ( reach.Hit || source.GetComponent<BaseInventoryComponent>() is not { } inventory ) return;

		var className = GrenadeModel switch
		{
			"grenade" => "handgrenade_ex",
			"mills" => "mills_ex",
			"stick" => "stickgrenade_ex",
			_ => null
		};
		if ( className is null || GameObject.GetPrefab( $"mount://dod/entities/items/weapon_{className}.prefab" ) is not { IsValid: true } prefab ) return;

		if ( inventory.Pickup( prefab ) is not LiveGrenadeWeapon weapon ) return;

		inventory.Switch( weapon );
		if ( inventory.ActiveItem != weapon )
		{
			weapon.SetFuse( null );
			inventory.Remove( weapon );
			return;
		}

		weapon.SetFuse( FuseRemaining );
		_claimed = true;
		GameObject.Enabled = false;
		GameObject.Destroy();
	}

	internal override void Detonate() => Explode( GameObject, Thrower, WorldPosition );

	internal static void Explode( GameObject inflictor, GameObject thrower, Vector3 position )
	{
		var floor = inflictor.Scene.Trace.Ray( position + Vector3.Up * 8, position + Vector3.Down * 32 )
			.IgnoreGameObjectHierarchy( inflictor ).WithoutTags( "trigger", "debris", "playercontroller", "npc", "player" ).Run();
		var origin = floor.Hit && (position - floor.EndPosition).Dot( floor.Normal ) <= 8
			? floor.EndPosition + floor.Normal * 8 : position;
		DayOfDefeatExplosion.Detonate( inflictor, thrower, origin, floor.EndPosition, floor.Normal, floor.Hit, BlastDamage, 4.8f );
	}
}

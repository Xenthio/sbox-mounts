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
		Blast.DamageInRadius( inflictor, thrower, origin, BlastDamage, BlastDamage * 2.5f, "blast" );
		var underwater = WaterBody.Find( inflictor.Scene, origin ).IsValid();
		ContentMount?.NpcAudio.Emit( underwater ? "weapons/explodewater.wav" : $"weapons/explode{Game.Random.Int( 3, 5 )}.wav", origin );
		ShowExplosion( origin, floor.EndPosition, floor.Normal, floor.Hit, underwater );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static async void ShowExplosion( Vector3 origin, Vector3 contact, Vector3 normal, bool hit, bool underwater )
	{
		if ( Application.IsDedicatedServer || ContentMount is not { } mount ) return;
		var scene = Game.ActiveScene;
		var fireball = Blast.SpriteEffect( "DoD grenade explosion", "zerogxplode", origin, 4.8f, 15, mount: mount );
		ExplosionParticles.Spawn( origin, fireball );
		if ( hit ) ImpactDecal.Place( contact, normal, $"scorch{Game.Random.Int( 1, 2 )}", 48, mount );
		if ( !underwater )
		{
			var sparks = Game.Random.Int( 0, 3 );
			for ( var i = 0; i < sparks; i++ ) SparkShower.Spawn( origin, hit ? normal : Vector3.Zero, mount );
		}

		await GameTask.DelaySeconds( 0.3f );
		if ( !scene.IsValid() || scene != Game.ActiveScene ) return;
		if ( underwater ) Blast.Bubbles( origin, 64, 100, "DoD grenade bubbles", mount );
		else Blast.SpriteEffect( "DoD grenade smoke", "steam1", origin + Vector3.Up * 20, 4.8f, 12, additive: false, mount: mount );
	}
}

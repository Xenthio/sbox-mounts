using System;
using GoldSrc.HalfLife;

namespace GoldSrc.TeamFortressClassic;

[Title( "EMP Grenade" ), Category( "Team Fortress Classic" )]
[WeaponPickup( "grenade", "tfc_emp_grenades", 2, EntityName = "tf_weapon_empgrenade",
	PickupModel = "emp_grenade", Bucket = 4, Mount = "tfc" )]
public sealed class EmpGrenade : FortressHandGrenadeWeapon
{
	internal override void Configure()
	{
		base.Configure();
		StartingAmmo = 2;
	}

	internal override string LaunchGrenade( Vector3 origin, Vector3 direction, float pitch, float pinAge ) =>
		Launch<FortressEmpGrenade>( origin, pinAge, "TFC EMP grenade", "models/emp_grenade.mdl" );
}

[Hide]
public sealed class FortressEmpGrenade : FortressHandGrenade
{
	const float Radius = 240;
	static readonly (string Name, float DamagePerRound)[] ExplosiveAmmo = [("buckshot", 0.75f), ("rockets", 1.5f), ("uranium", 1.5f)];
	const string AmmoPrefix = "mount://tfc/ammo/";

	internal override void Detonate()
	{
		var origin = WorldPosition;
		Mount?.NpcAudio.Emit( "weapons/emp_1.wav", origin, attenuation: 0.8f );
		ShowPulse( origin );

		foreach ( var pipe in Scene.GetAllComponents<FortressGrenade>() )
			if ( InRange( pipe.WorldPosition ) ) pipe.TriggerEmp();

		foreach ( var player in Scene.GetAllComponents<PlayerController>().ToArray() )
		{
			if ( !player.IsValid() || !player.Enabled || Maps.MapState.PlayerVitals( player ) is { Health: <= 0 } ) continue;
			var center = FortressHandGrenadeWeapon.HullCenter( player );
			if ( !InRange( center ) || player.GetComponentInChildren<BaseInventoryComponent>() is not { } inventory ) continue;
			var damage = DrainReserve( inventory );
			if ( damage > 0 ) CookOff( center, damage, player.GameObject );
		}

		foreach ( var pickup in Scene.GetAllComponents<BaseAmmoPickup>().ToArray() )
		{
			if ( !pickup.IsValid() || pickup.Amount <= 0 || !InRange( pickup.WorldPosition ) ) continue;
			var path = pickup.AmmoType?.ResourcePath;
			var weight = ExplosiveAmmo.FirstOrDefault( ammo => path == $"{AmmoPrefix}{ammo.Name}.ammo" ).DamagePerRound;
			if ( weight <= 0 ) continue;
			var position = pickup.WorldPosition;
			var damage = pickup.Amount * weight;
			pickup.Amount = 0;
			CookOff( position, damage, pickup.GameObject );
			pickup.GameObject.Destroy();
		}
	}

	bool InRange( Vector3 position ) => WorldPosition.DistanceSquared( position ) <= Radius * Radius;

	static float DrainReserve( BaseInventoryComponent inventory )
	{
		var damage = 0f;
		foreach ( var (name, damagePerRound) in ExplosiveAmmo )
		{
			var ammo = GameResource.Load<BaseAmmoResource>( $"{AmmoPrefix}{name}.ammo" );
			var rounds = inventory.GetAmmo( ammo );
			if ( rounds <= 0 || rounds == int.MaxValue ) continue;
			damage += rounds * damagePerRound;
			inventory.TakeAmmo( ammo, (rounds - 1) / 4 + 1 );
		}
		return damage;
	}

	void CookOff( Vector3 position, float damage, GameObject sourceBody = null )
	{
		FortressBlast.Apply( GameObject, Thrower, position, damage, falloff: 0.5f, sourceBody: sourceBody );
		Mount?.NpcAudio.Emit( $"weapons/explode{Game.Random.Int( 3, 5 )}.wav", position );
		ShowCookOff( position, Math.Clamp( (int)(damage * 0.75f), 5, 255 ) * 0.1f );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void ShowPulse( Vector3 position )
	{
		if ( Mount is { } mount ) SonicRing.Spawn( position + Vector3.Up * 16, Color.White, 600, mount, width: 70 );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void ShowCookOff( Vector3 position, float scale )
	{
		if ( Mount is { } mount ) Blast.SpriteEffect( "TFC EMP ammunition explosion", "explode01", position, scale, 15,
			additive: false, mount: mount );
	}
}

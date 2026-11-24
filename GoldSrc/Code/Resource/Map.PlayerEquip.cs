using System;
using GoldSrc.Bsp;

namespace GoldSrc.Maps;

partial class MapBuilder
{
	private void SpawnPlayerEquip( GameObject go, Entity entity )
	{
		var equip = go.AddComponent<PlayerEquip>();
		equip.TargetName = entity.TargetName;
		equip.UseOnly = (entity.SpawnFlags & 1) != 0;
		foreach ( var (key, value) in entity.KeyValues )
		{
			var delimiter = key.IndexOf( '#' );
			var name = (delimiter < 0 ? key : key[..delimiter]).ToLowerInvariant();
			var count = int.TryParse( value, out var parsed ) ? Math.Max( 1, parsed ) : 1;
			if ( name is "item_suit" or "item_longjump" or "item_battery" or "item_healthkit" )
				AddGrant( equip.SuitItems, name, count );
			else if ( name.StartsWith( "weapon_", StringComparison.Ordinal ) || name.StartsWith( "tf_weapon_", StringComparison.Ordinal ) )
			{
				if ( GetWeaponPickupPath( name ) is { } path ) AddGrant( equip.Weapons, path, count );
			}
			else if ( name.StartsWith( "ammo_", StringComparison.Ordinal ) )
			{
				var (_, pool, rounds) = GetAmmoPickup( name, GetCustomAmmo( name ) );
				if ( pool is null ) continue;
				var path = $"mount://{Host.Ident}/ammo/{pool}.ammo";
				if ( Host.GetByPath( path ) is not null )
					AddGrant( equip.Ammo, path, (long)count * rounds );
			}
		}
		RegisterTarget( entity, equip );
	}

	private static void AddGrant( Dictionary<string, int> grants, string name, long count ) =>
		grants[name] = (int)Math.Min( int.MaxValue, grants.GetValueOrDefault( name ) + count );
}

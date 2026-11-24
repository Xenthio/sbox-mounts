using GoldSrc.Bsp;
using GoldSrc.HalfLife;

namespace GoldSrc.Maps;

partial class MapBuilder
{
	private bool TrySpawnPickup( Entity entity )
	{
		var name = entity.ClassName.ToLowerInvariant();
		var halfLifeItems = Host is HalfLifeMount or HalfLifeMModMount or HalfLifeModMount;
		var weapon = name.StartsWith( "weapon_", System.StringComparison.Ordinal ) ||
			name.StartsWith( "tf_weapon_", System.StringComparison.Ordinal );
		(string Model, string Pool, int Amount) customAmmo = (Host, name) switch
		{
			(CryOfFearMount, "ammo_glock") => ("ammo/clip", "glock", 15),
			(CryOfFearMount, "ammo_shells") => ("ammo/ammo_shotshells", "buckshot", 9),
			(CryOfFearMount, "ammo_p345") => ("ammo/p345_clip", "p345", 8),
			(CryOfFearMount, "ammo_vp70") => ("ammo/vp70_clip", "vp70", 18),
			(ScientistSlaughterhouseMount, "ammo_sniperclip") => ("w_m40a1clip", "m40", 5),
			(ScientistSlaughterhouseMount, "ammo_egonclip") => ("w_isotopebox", "uranium", 250),
			(ScientistSlaughterhouseMount, "ammo_gaussclip") => ("w_gaussammo", "uranium", 100),
			(ScientistSlaughterhouseMount, "ammo_vortexclip") => ("w_rpgammo", "vortex_m", 5),
			(ScientistSlaughterhouseMount, "ammo_fireballclip") => ("w_weaponbox", "ffuel", 20),
			(ScientistSlaughterhouseMount, "ammo_acidtank") => ("w_weaponbox", "acid", 30),
			(ScientistSlaughterhouseMount, "ammo_laserclip") => ("w_9mmclip", "uranium", 20),
			(CrackLifeCampaignMount, "ammo_snipars") => ("w_357ammobox", "snipars", 10),
			(CrackLifeCampaignMount, "ammo_gaussclip") => ("w_gaussammo", "uranium", 999),
			(CrackLifeCampaignMount, "ammo_crossbow") => ("w_crossbow_clip", "bolts", 50),
			_ => default
		};
		if ( !weapon && !halfLifeItems && customAmmo.Model is null ) return false;
		GameObject go;
		var transform = new Transform( entity.Origin, entity.Rotation );
		if ( name == "world_items" && entity.GetInt( "type" ) == 45 ) name = "item_suit";
		var itemModel = name switch
		{
			"item_suit" => "w_suit", "item_longjump" => "w_longjump", "item_battery" => "w_battery",
			"item_healthkit" => "w_medkit", "weaponbox" => "w_weaponbox", _ => null
		};
		if ( itemModel is not null )
		{
			go = new GameObject( false, EntityName( entity ) ) { WorldTransform = transform };
			go.AddComponent<ModelRenderer>().Model = LoadMountedModel( $"models/{itemModel}.mdl" );
			var trigger = go.AddComponent<BoxCollider>();
			trigger.Center = Vector3.Up * 8;
			trigger.Scale = new Vector3( 32, 32, 16 );
			trigger.IsTrigger = true;
			var item = go.AddComponent<SuitItem>();
			item.Item = name;
			item.ShortLogon = (entity.SpawnFlags & 1) != 0;
			if ( name == "weaponbox" )
			{
				foreach ( var (key, value) in entity.KeyValues )
				{
					if ( !int.TryParse( value, out var count ) || count <= 0 || EntityFields.Contains( key ) ) continue;
					var pool = key.ToLowerInvariant() switch { "trip mine" => "tripmines", "satchel charge" => "satchels", _ => key.ToLowerInvariant() };
					var ammoPath = $"mount://{Host.Ident}/ammo/{pool}.ammo";
					if ( Host.GetByPath( ammoPath ) is not null ) item.Ammo[ammoPath] = count;
				}
			}
			item.TargetName = entity.TargetName;
			RegisterTarget( entity, item, null, null, null, nameof( MapEntity.Kill ) );
			WireTrigger( entity, item );
			go.Enabled = true;
			return true;
		}
		if ( weapon )
		{
			var canonical = !halfLifeItems ? name : name switch { "weapon_glock" => "weapon_9mmhandgun", "weapon_mp5" => "weapon_9mmar", "weapon_python" => "weapon_357", _ => name };
			var path = $"mount://{Host.Ident}/entities/items/{canonical}.prefab";
			if ( Host.GetByPath( path ) is not WeaponPickupLoader ) return false;
			go = GameObject.Clone( PrefabFile.Load( path ), transform, startEnabled: false );
		}
		else
		{
			var (model, pool, amount) = customAmmo.Model is not null ? customAmmo : name switch
			{
				"ammo_glockclip" or "ammo_9mmclip" => ("w_9mmclip", "9mm", 17),
				"ammo_mp5clip" or "ammo_9mmar" => ("w_9mmARclip", "9mm", 50),
				"ammo_9mmbox" => ("w_chainammo", "9mm", 200),
				"ammo_mp5grenades" or "ammo_argrenades" => ("w_ARgrenade", "argrenades", 2),
				"ammo_buckshot" => ("w_shotbox", "buckshot", 12),
				"ammo_357" => ("w_357ammobox", "357", 6),
				"ammo_gaussclip" => ("w_gaussammo", "uranium", 20),
				"ammo_crossbow" => ("w_crossbow_clip", "bolts", 5),
				"ammo_rpgclip" => ("w_rpgammo", "rockets", 1),
				_ => (null, null, 0)
			};
			if ( model is null ) return false;
			var ammoPath = $"mount://{Host.Ident}/ammo/{pool}.ammo";
			var requiresAmmoResource = Host is HalfLifeMModMount or HalfLifeModMount or CryOfFearMount;
			if ( requiresAmmoResource && Host.GetByPath( ammoPath ) is null ) return false;
			var resource = LoadMountedModel( $"models/{model}.mdl" );
			if ( resource is not { IsError: false } ) return false;
			var ammo = GameResource.Load<BaseAmmoResource>( ammoPath );
			if ( requiresAmmoResource && ammo is null ) return false;
			go = HalfLife.AmmoPickup.Create( transform, resource, ammo, amount, pool );
			var pickup = go.GetComponent<HalfLife.AmmoPickup>();
			if ( Host is HalfLifeModMount or CryOfFearMount )
				pickup.SoundMount = Host.Ident;
			if ( Host is CryOfFearMount )
				pickup.PickupSound = "weapons/item_get.wav";
		}

		go.Name = EntityName( entity );
		{
			var marker = go.AddComponent<Relay>();
			marker.TargetName = entity.TargetName;
			RegisterTarget( entity, marker, null, null, null, nameof( MapEntity.Kill ) );
			WireTrigger( entity, marker );
		}
		go.Enabled = true;
		return true;
	}
}

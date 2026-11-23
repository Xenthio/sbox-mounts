using GoldSrc.HalfLife;

namespace GoldSrc;

public abstract partial class GameMount
{
	private static void MountWeapons( MountContext context, Dictionary<string, string> models, (TypeDescription Type, WeaponPickupAttribute Pickup)[] weapons )
	{
		foreach ( var ammo in weapons.Select( weapon => weapon.Pickup ).Where( pickup => pickup.Ammo is not null ).DistinctBy( pickup => pickup.Ammo ) )
			context.Add( ResourceType.GameResource, $"ammo/{ammo.Ammo}.ammo", new AmmoLoader( ammo.Ammo, ammo.Limit ) );

		foreach ( var (type, pickup) in weapons )
		{
			if ( !models.TryGetValue( pickup.WorldPickupPath, out var model ) ) continue;
			var className = Path.GetFileNameWithoutExtension( pickup.PrefabPath );
			var icon = new WeaponIconLoader( className );
			context.Add( ResourceType.Texture, $"icons/{className}", icon );
			var preview = pickup.PreviewModel is null ? model : models.GetValueOrDefault( $"models/{pickup.PreviewModel}.mdl", model );
			if ( pickup.PreviewPitch != 0 )
			{
				var upright = $"previews/{className}";
				context.Add( ResourceType.Model, upright, new ModelLoader( preview, previewRotation: Rotation.FromPitch( pickup.PreviewPitch ) ) );
				preview = upright;
			}
			var loader = new WeaponPickupLoader( type, model, preview, icon.Path );
			loader.Tags.Add( "weapon" );
			context.Add( ResourceType.PrefabFile, pickup.PrefabPath, loader );
		}
	}
}

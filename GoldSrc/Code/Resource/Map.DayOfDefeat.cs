using System;
using GoldSrc.Bsp;
using GoldSrc.DayOfDefeat;

namespace GoldSrc.Maps;

partial class MapBuilder
{
	private void SpawnMachineGunNest( Entity entity, BspModel model )
	{
		// GoldSrc sets the trigger size to the BSP model bounds. Facing is independent of the brush.
		var center = (model.Mins + model.Maxs) * 0.5f;
		var go = new GameObject( true, EntityName( entity ) ) { IsStatic = true, WorldPosition = entity.Origin + center };
		var nest = go.AddComponent<MachineGunNest>();
		nest.Bounds = new BBox( model.Mins - center, model.Maxs - center );
		nest.FacingYaw = entity.Angles.yaw;
		nest.FacingRange = Math.Clamp( entity.GetInt( "sandbag_range", 5 ), 0, 180 );
		nest.TargetName = entity.TargetName;
		RegisterTarget( entity, nest, null, null, null, nameof( MapEntity.Kill ) );
	}
}

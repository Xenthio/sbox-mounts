using System;
using GoldSrc.Bsp;
using GoldSrc.DayOfDefeat;

namespace GoldSrc.Maps;

partial class MapBuilder
{
	private bool SpawnDoDObjective( Entity entity )
	{
		if ( entity.ClassName is not ("dod_control_point" or "dod_control_point_master" or "dod_point_relay") ) return false;
		var go = new GameObject( true, EntityName( entity ) ) { WorldPosition = entity.Origin, WorldRotation = entity.Rotation };
		switch ( entity.ClassName )
		{
			case "dod_control_point":
				var point = go.AddComponent<ControlPoint>();
				point.TargetName = entity.TargetName;
				point.Group = entity.GetString( "point_group", "" );
				point.TeamPoints = entity.GetInt( "point_team_points" );
				point.Neutral = ReadPointState( entity, "reset" );
				point.Allies = ReadPointState( entity, "allies" );
				point.Axis = ReadPointState( entity, "axis" );
				point.Renderer = go.AddComponent<SkinnedModelRenderer>();
				point.Renderer.UseAnimGraph = false;
				point.Owner = ReadObjectiveTeam( entity, "point_default_owner" );
				RegisterTarget( entity, point, null, null, null, nameof( MapEntity.Kill ) );
				_wiring.Add( () =>
				{
					point.Neutral.Output = BuildFire( entity.GetString( "point_reset_target" ) );
					point.Allies.Output = BuildFire( entity.GetString( "point_allies_target" ) );
					point.Axis.Output = BuildFire( entity.GetString( "point_axis_target" ) );
				} );
				break;

			case "dod_control_point_master":
				var master = go.AddComponent<ControlPointMaster>();
				master.TargetName = entity.TargetName;
				master.Group = entity.GetString( "cpm_group", "" );
				RegisterTarget( entity, master, null, null, null, nameof( MapEntity.Kill ) );
				_wiring.Add( () =>
				{
					master.OnAlliesCapture = BuildFire( entity.GetString( "allies_capture_target" ) );
					master.OnAxisCapture = BuildFire( entity.GetString( "axis_capture_target" ) );
				} );
				break;

			case "dod_point_relay":
				var relay = go.AddComponent<PointRelay>();
				relay.TargetName = entity.TargetName;
				relay.Team = ReadObjectiveTeam( entity, "dod_relay_team" );
				RegisterTarget( entity, relay );
				_wiring.Add( () => relay.Point = _mapState.Find( entity.Target ) as ControlPoint );
				break;
		}
		return true;
	}

	private ControlPoint.TeamState ReadPointState( Entity entity, string team )
	{
		var model = entity.GetString( $"point_{team}_model" );
		var sound = entity.GetString( $"point_{team}_capsound" );
		return new()
		{
			Model = string.IsNullOrEmpty( model ) ? null : LoadMountedModel( model ),
			BodyGroup = entity.GetInt( $"point_{team}_model_bodygroup" ),
			CaptureSound = string.IsNullOrEmpty( sound ) ? null : BuildSoundEvent( sound )
		};
	}

	private static ObjectiveTeam ReadObjectiveTeam( Entity entity, string key ) =>
		(ObjectiveTeam)Math.Clamp( entity.GetInt( key ), 0, 2 );

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

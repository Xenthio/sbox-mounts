using System;
using GoldSrc.Maps;

namespace GoldSrc.DayOfDefeat;

[Hide]
public sealed class MachineGunNest : MapEntity
{
	[Property] public BBox Bounds { get; set; }
	[Property] public float FacingYaw { get; set; }
	[Property] public float FacingRange { get; set; } = 5;

	internal static MachineGunNest Find( PlayerController player )
	{
		var hull = PlayerBounds( player );
		foreach ( var nest in player.Scene.GetAllComponents<MachineGunNest>() )
		{
			var yaw = (nest.WorldRotation * Rotation.FromYaw( nest.FacingYaw )).Yaw();
			if ( MathF.Abs( Angles.NormalizeAngle( player.EyeAngles.yaw - yaw ) ) > nest.FacingRange ) continue;
			if ( nest.Supports( player, hull ) ) return nest;
		}
		return null;
	}

	internal bool Supports( PlayerController player ) => Supports( player, PlayerBounds( player ) );

	bool Supports( PlayerController player, BBox hull ) =>
		Active && CanTrigger && Bounds.Transform( WorldTransform ).Overlaps( hull ) && IsMasterTriggered( player.GameObject );

	static BBox PlayerBounds( PlayerController player ) =>
		player.BodyBox( MathF.Abs( player.WorldTransform.UniformScale ) ) + player.WorldPosition;
}

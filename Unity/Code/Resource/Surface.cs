using System.Runtime.CompilerServices;

namespace UnityMount;

readonly record struct UnitySurfaceDefinition( long Id, string Name, string Tags, AudioSurface AudioSurface,
	Color FootstepTint, UnityAssetPointer? FootstepEffect, float SpeedBoost );

sealed record UnitySurfaceFootstep( Color Tint, string EffectPath, float SpeedBoost )
{
	internal static UnitySurfaceFootstep For( Surface surface ) => surface is null ? null : Metadata.TryGetValue( surface, out var data ) ? data : null;
	internal static readonly ConditionalWeakTable<Surface, UnitySurfaceFootstep> Metadata = new();
}

sealed class UnitySurfaceLoader( UnitySurfaceDefinition definition, string footstepEffectPath ) : ResourceLoader<GameMount>
{
	protected override object Load()
	{
		const string defaultPath = "surfaces/default.surface";
		var original = GameResource.Load<Surface>( defaultPath );
		var surface = new Surface
		{
			Description = definition.Name,
			Tags = definition.Tags,
			AudioSurface = definition.AudioSurface,
			BaseSurface = defaultPath,
			Friction = original.Friction,
			Elasticity = original.Elasticity,
			Density = original.Density,
			RollingResistance = original.RollingResistance,
			BounceThreshold = original.BounceThreshold,
			FluidLinearDrag = original.FluidLinearDrag,
			FluidAngularDrag = original.FluidAngularDrag
		};
		UnitySurfaceFootstep.Metadata.Add( surface, new( definition.FootstepTint, footstepEffectPath, definition.SpeedBoost ) );
		return surface;
	}
}

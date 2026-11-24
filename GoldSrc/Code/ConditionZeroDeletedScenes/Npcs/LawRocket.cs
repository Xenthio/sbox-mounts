using GoldSrc.HalfLife;

namespace GoldSrc.ConditionZeroDeletedScenes;

[Hide]
public sealed class LawRocket : PlayerRocket
{
	internal override float LaunchSpeed => 250;
	internal override float IgnitionDelay => 0;
	internal override GameMount EffectMount => ModelMount;
	internal override GameMount BlastMount => ModelMount;

	protected override Vector3 FindSpotDirection() => WorldRotation.Forward;

	internal override SceneTrace FlightTrace( Vector3 start, Vector3 end ) =>
		base.FlightTrace( start, end ).WithoutTags( "trigger" );
}

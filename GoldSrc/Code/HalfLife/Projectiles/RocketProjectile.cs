namespace GoldSrc.HalfLife;

[Hide]
public abstract class RocketProjectile : NpcProjectile
{
	internal abstract float IgnitionDelay { get; }
	internal abstract float SmokeLifetime { get; }
	internal virtual float BurnTime => float.PositiveInfinity;
	internal virtual GameMount EffectMount => Sandbox.Mounting.Directory.Get( "hl1" ) as HalfLifeMount;
	bool _ignited;
	SoundHandle _motor;
	PointLight _lamp;

	protected override void OnUpdate()
	{
		if ( !_ignited && Age >= IgnitionDelay && EffectMount is { } mount )
		{
			_ignited = true;
			ProjectileTrail.Attach( GameObject, mount, "Rocket exhaust", "smoke", SmokeLifetime, 5, new Color32( 224, 224, 255, 255 ) );
			_lamp = GameObject.AddComponent<PointLight>();
			_lamp.Radius = 96;
			_lamp.Shadows = false;
			if ( mount.NpcAudio.Find( "weapons/rocket1.wav" ) is { } path && SoundFile.Load( path ) is { } sound )
				_motor = GoldSrcSoundEvents.Play( sound, WorldPosition, attenuation: 0.5f );
		}
		if ( Age >= IgnitionDelay + BurnTime )
		{
			_motor?.Stop();
			_motor = null;
			if ( _lamp.IsValid() ) _lamp.Enabled = false;
		}
		else if ( _motor.IsValid() ) _motor.Position = WorldPosition;
	}

	protected override void OnDisabled() => _motor?.Stop();
}

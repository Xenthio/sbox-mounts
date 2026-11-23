using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Hide]
public sealed class DayOfDefeatShell : NpcProjectile
{
	const float BlastDamage = 100;
	SoundHandle _motor;
	bool _startedMotor;
	TimeUntil _nextPuff;

	internal override float LaunchSpeed => 1300;
	internal override float Lifetime => 10;
	internal override string HitTag => "blast";

	internal static void Launch( GameObject owner, string model, Vector3 origin, Vector3 direction ) =>
		Fire<DayOfDefeatShell>( owner, model, BlastDamage, null, origin, direction, null );

	internal override void Guide() => Velocity += Scene.PhysicsWorld.Gravity * (0.1f * Time.Delta);

	internal override SceneTrace FlightTrace( Vector3 start, Vector3 end ) =>
		base.FlightTrace( start, end ).WithoutTags( "trigger", "playercontroller", "debris" );

	internal override void HandleContact( SceneTraceResult hit )
	{
		if ( hit.GameObject?.GetComponentInParent<Maps.BreakableBrush>() is { MaterialType: 0, Pushable: false } glass )
		{
			// A rocket breaks ordinary glass and continues with its current velocity.
			var impact = new DamageInfo { Attacker = Shooter, Weapon = GameObject, Damage = glass.Health, Position = hit.EndPosition };
			impact.Tags.Add( "dod_rocket_impact" );
			((IDamageable)glass).OnDamage( impact );
			if ( glass.Health <= 0 && !glass.TriggerOnly )
			{
				WorldPosition = hit.EndPosition + Velocity.Normal;
				return;
			}
			GameObject.Destroy();
			return;
		}

		if ( hit.Surface?.Tags != GoldSrcSkySurfaceLoader.SkyTag )
		{
			var position = hit.EndPosition + hit.Normal * 8;
			DayOfDefeatExplosion.Detonate( GameObject, Shooter, position, hit.EndPosition, hit.Normal, true, Damage, 3, rocket: true );
		}
		GameObject.Destroy();
	}

	protected override void OnUpdate()
	{
		if ( Application.IsDedicatedServer || Sandbox.Mounting.Directory.Get( "dod" ) is not DayOfDefeatMount mount ) return;
		if ( !_startedMotor )
		{
			_startedMotor = true;
			var sample = ModelPath.EndsWith( "/w_piat_rocket.mdl.vmdl", System.StringComparison.Ordinal ) ? "rocket2" : "rocket1";
			_motor = NpcAudio.Play( mount.NpcAudio.Find( $"weapons/{sample}.wav" ), WorldPosition, attenuation: 0.5f );
		}
		if ( _motor.IsValid() ) _motor.Position = WorldPosition;
		if ( _nextPuff > 0 || WaterBody.Find( Scene, WorldPosition ).IsValid() ) return;

		_nextPuff = 0.02f;
		var exhaust = WorldPosition - WorldRotation.Forward * 12;
		Blast.SpriteEffect( "DoD rocket exhaust", "effects/adrian/bazooka_smoke", exhaust, 0.3f, 30, additive: false, mount: mount );
	}

	protected override void OnDisabled() => _motor?.Stop();
}

using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Hide]
public sealed class SmokeGrenadeProjectile : CounterStrikeTimedGrenade
{
	const int LastPulse = 21;
	const float FirstPulse = 0.1f;

	[Sync( SyncFlags.FromHost )] internal TimeSince SmokeAge { get; private set; }
	[Sync( SyncFlags.FromHost )] internal bool Emitting { get; private set; }
	[Sync( SyncFlags.FromHost )] internal bool SubmergedAtStart { get; private set; }
	[Sync( SyncFlags.FromHost )] internal Vector3 CloudOrigin { get; private set; }
	[Sync( SyncFlags.FromHost )] internal int CloudSeed { get; private set; }

	bool _cloudShown;
	int _lastPulse = -1;

	internal static void Throw( GameObject owner, Vector3 origin, Vector3 velocity, GameMount mount ) =>
		ThrowTimed<SmokeGrenadeProjectile>( owner, origin, velocity, mount, "Smoke grenade", "w_smokegrenade" );

	internal override bool ReadyToDetonate( Rigidbody body )
	{
		if ( !body.IsValid() || System.MathF.Abs( body.Velocity.z ) > 30 ) return false;

		// Check just below the canister's collision bounds. The fuse can expire while it is still falling.
		var bottom = WorldPosition.WithZ( body.GetWorldBounds().Mins.z );
		var support = TraceCover( WorldPosition + Vector3.Up * 8, bottom + Vector3.Down * 4 ).WithoutTags( "water" ).Run();
		return support.Hit && support.Normal.z > 0.7f;
	}

	internal override void OnTumble( Rigidbody body )
	{
		if ( ReadyToDetonate( body ) && !body.Sleeping ) body.Velocity *= 0.95f;
	}

	internal override void OnFuseExpired()
	{
		CloudOrigin = WorldPosition;
		CloudSeed = Game.Random.Int( 0, int.MaxValue - 1 );
		SubmergedAtStart = WaterBody.Find( Scene, WorldPosition ).IsValid();
		SmokeAge = 0;
		Emitting = true;
		ContentMount?.NpcAudio.Emit( "weapons/sg_explode.wav", CloudOrigin );
		var body = GetComponent<Rigidbody>();
		body.Velocity = new Vector3( Game.Random.Float( -175, 175 ), Game.Random.Float( -175, 175 ), Game.Random.Float( 250, 350 ) );
	}

	protected override void OnFixedUpdate()
	{
		if ( !Emitting ) base.OnFixedUpdate();
		else if ( Networking.IsHost && SmokeAge > FirstPulse + LastPulse + 0.1f ) GameObject.Destroy();
	}

	protected override void OnUpdate()
	{
		if ( Application.IsDedicatedServer || !Emitting || ContentMount is not { } mount ) return;
		float age = SmokeAge;
		if ( !_cloudShown )
		{
			_cloudShown = true;
			if ( !SubmergedAtStart ) SmokeGrenadePuff.ShowCloud( mount, CloudOrigin, CloudSeed, age );
		}

		if ( age < FirstPulse ) return;
		var pulse = (int)(age - FirstPulse);
		if ( pulse <= _lastPulse || pulse > LastPulse ) return;
		_lastPulse = pulse;
		if ( WaterBody.Find( Scene, WorldPosition ).IsValid() )
		{
			Blast.Bubbles( WorldPosition, 64, 100, "Smoke grenade bubbles", mount );
			return;
		}

		var direction = Rotation.FromYaw( CloudSeed % 360 + pulse * 30 ).Forward;
		SmokeGrenadePuff.ShowVent( mount, WorldPosition, direction );
	}
}

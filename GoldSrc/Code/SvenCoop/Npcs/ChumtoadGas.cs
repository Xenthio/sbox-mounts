using GoldSrc.HalfLife;

namespace GoldSrc.SvenCoop;

[Hide]
public sealed class ChumtoadGas : Component
{
	const float CloudDuration = 5;
	const float PuffDuration = 3.5f;

	ParticleEffect _particles;
	TimeSince _age;
	TimeUntil _nextPuff;

	protected override void OnStart() => _age = 0;

	[Rpc.Broadcast( NetFlags.HostOnly )]
	internal static void Show( Vector3 origin )
	{
		if ( Sandbox.Mounting.Directory.Get( "svencoop" ) is not SvenCoopMount mount ||
			(mount.GetIndexedSprite( "puff1" ) ?? mount.GetIndexedSprite( "steam1" )) is not { } sprite ) return;

		var go = new GameObject( false, "Chumtoad gas" ) { NetworkMode = NetworkMode.Never, WorldPosition = origin };
		var gas = go.AddComponent<ChumtoadGas>();
		gas._particles = go.AddComponent<ParticleEffect>();
		gas._particles.Lifetime = PuffDuration;
		gas._particles.ApplyShape = true;
		gas._particles.InitialScale = 0.6f;
		go.AddComponent<ParticleSpriteRenderer>().Sprite = sprite;
		go.AddComponent<TemporaryEffect>().DestroyAfterSeconds = CloudDuration + PuffDuration;
		go.Enabled = true;
	}

	protected override void OnUpdate()
	{
		if ( _age >= CloudDuration || _nextPuff > 0 ) return;
		_nextPuff = 0.1f;
		var origin = WorldPosition + Vector3.Up * 8;
		if ( _particles.Emit( origin, 0 ) is not { } puff ) return;
		puff.Velocity = new Vector3( Game.Random.Float( -64, 64 ), Game.Random.Float( -64, 64 ), 64 );
		puff.Color = new Color32( 0, (byte)Game.Random.Int( 150, 255 ), 0, 255 );
		puff.Alpha = 0.5f;
	}
}

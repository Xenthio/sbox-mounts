using GoldSrc.HalfLife;

namespace GoldSrc.TeamFortressClassic;

[Hide]
public sealed class GasHallucination : Component
{
	const int FullDuration = 60;
	const int ExposureExtension = 10;
	const float Interval = 0.3f;
	static readonly string[] ImaginedGrenades = ["mirv_grenade", "w_grenade", "napalm", "ngrenade"];

	PlayerController _player;
	int _episodesRemaining;
	TimeUntil _nextEpisode;

	internal static void Expose( PlayerController player )
	{
		var effect = player.GetComponentInChildren<GasHallucination>();
		if ( !effect.IsValid() )
		{
			var go = new GameObject( player.GameObject, true, "TFC gas hallucination" ) { NetworkMode = NetworkMode.Never };
			effect = go.AddComponent<GasHallucination>();
			effect._player = player;
			effect._episodesRemaining = FullDuration;
		}
		else
		{
			effect._episodesRemaining = System.Math.Min( FullDuration, effect._episodesRemaining + ExposureExtension );
		}
		effect._nextEpisode = Interval;
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return;
		if ( !_player.IsValid() || !_player.Enabled || Maps.MapState.PlayerVitals( _player ) is { Health: <= 0 } )
		{
			GameObject.Destroy();
			return;
		}
		if ( _nextEpisode > 0 ) return;
		if ( --_episodesRemaining == 0 )
		{
			GameObject.Destroy();
			return;
		}

		_nextEpisode = Interval;
		var roll = Game.Random.Int( 0, 20 );
		if ( roll > (int)Episode.Tracer ) return;
		using ( Rpc.FilterInclude( _player.Network.Owner ?? Connection.Local ) )
			PlayEpisode( _player, (Episode)roll );
	}

	enum Episode { MetalHit, FleshHit, Explosion, Grenade, Flames, Nail, Tracer }

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void PlayEpisode( PlayerController player, Episode episode )
	{
		if ( Application.IsDedicatedServer || !player.IsValid() || player.IsProxy ||
			Sandbox.Mounting.Directory.Get( "tfc" ) is not GameMount mount ) return;
		var eye = player.EyeTransform;
		var nearby = eye.Position + eye.Forward * Game.Random.Float( 100, 300 ) + eye.Right * Game.Random.Float( -100, 100 );

		switch ( episode )
		{
			case Episode.MetalHit:
				NpcAudio.Play( mount.NpcAudio.Find( "weapons/cbar_hit1.wav" ), eye.Position );
				break;
			case Episode.FleshHit:
				NpcAudio.Play( mount.NpcAudio.Find( "weapons/cbar_hitbod1.wav" ), eye.Position );
				if ( player.Scene.Camera is { IsValid: true } camera )
					player.Scene.GetSystem<CameraEffectSystem>().AddShake( camera, 12, 10, 0.9f );
				break;
			case Episode.Explosion:
				Blast.SpriteEffect( "TFC imagined explosion", "explode01", nearby, 2, 12, additive: false, mount: mount );
				NpcAudio.Play( mount.NpcAudio.Find( $"weapons/explode{Game.Random.Int( 3, 5 )}.wav" ), nearby );
				break;
			case Episode.Grenade:
				var model = ImaginedGrenades[Game.Random.Int( 0, ImaginedGrenades.Length - 1 )];
				GasHallucinationProjectile.Show( mount, model, nearby, (eye.Position - nearby).Normal * 300 + Vector3.Up * 100, 2.5f, true );
				break;
			case Episode.Flames:
				var flame = Blast.SpriteEffect( "TFC imagined fire", "playerflame",
					FortressHandGrenadeWeapon.HullCenter( player ) + Vector3.Up * 5, 1, 15, mount: mount );
				if ( flame.IsValid() )
				{
					flame.SetParent( player.GameObject, true );
					flame.GetComponent<TemporaryEffect>().DestroyAfterSeconds = 2.5f;
				}
				break;
			case Episode.Nail:
				var behind = eye.Position - eye.Forward * 200 + eye.Right * Game.Random.Float( -100, 100 );
				GasHallucinationProjectile.Show( mount, "nail", behind, (nearby - behind).Normal * 1000, 0.6f, false );
				break;
			case Episode.Tracer:
				BulletEffects.ShowTracer( nearby, eye.Position + eye.Right * Game.Random.Float( -50, 50 ) );
				break;
		}
	}
}

[Hide]
public sealed class GasHallucinationProjectile : Component
{
	Vector3 _velocity;
	bool _grenade;

	internal static void Show( GameMount mount, string modelName, Vector3 position, Vector3 velocity, float duration, bool grenade )
	{
		if ( mount.FindMountPath( $"models/{modelName}.mdl" ) is not { } path ) return;
		var model = Model.Load( $"mount://tfc/{path}.vmdl" );
		if ( !model.IsValid() || model.IsError ) return;
		var go = new GameObject( false, "TFC imagined projectile" ) { NetworkMode = NetworkMode.Never };
		go.WorldTransform = new Transform( position, Rotation.LookAt( velocity ) );
		go.AddComponent<ModelRenderer>().Model = model;
		go.AddComponent<TemporaryEffect>().DestroyAfterSeconds = duration;
		var projectile = go.AddComponent<GasHallucinationProjectile>();
		projectile._velocity = velocity;
		projectile._grenade = grenade;
		go.Enabled = true;
	}

	protected override void OnUpdate()
	{
		if ( _grenade ) _velocity += Scene.PhysicsWorld.Gravity * Time.Delta;
		var trace = Scene.Trace.Ray( WorldPosition, WorldPosition + _velocity * Time.Delta )
			.IgnoreDynamic().WithoutTags( "trigger", "playercontroller" ).Run();
		WorldPosition = trace.EndPosition;
		if ( !trace.Hit ) return;
		if ( _grenade ) _velocity = Vector3.Reflect( _velocity, trace.Normal ) * 0.5f;
		else GameObject.Destroy();
	}
}

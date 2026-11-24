using GoldSrc.HalfLife;
using GoldSrc.Maps;
using Sandbox.Npcs;

namespace GoldSrc.ScientistSlaughterhouse;

[Hide]
public sealed class Rocket : PlayerRocket
{
	const float GuideInterval = 0.1f;
	const float Thrust = 256;
	const float HeatRange = 4096;
	float _nextGuide = GuideInterval;
	bool _released;

	[Sync( SyncFlags.FromHost )] internal RocketGuidance Guidance { get; set; }
	[Sync( SyncFlags.FromHost )] internal RocketWarhead Warhead { get; set; }
	internal override float IgnitionDelay => 0;
	internal override float BurnTime => float.PositiveInfinity;
	internal override Color32 SmokeTint => (Guidance, Warhead) switch
	{
		(_, RocketWarhead.Emp) => new( 128, 192, 255, 255 ),
		(RocketGuidance.Heatseeking, _) => new( 255, 224, 64, 255 ),
		(RocketGuidance.RemoteControlled, _) => new( 70, 140, 255, 255 ),
		(RocketGuidance.LaserGuided, RocketWarhead.Incendiary or RocketWarhead.PlasmaBomb) => new( 255, 128, 0, 255 ),
		(_, RocketWarhead.Incendiary or RocketWarhead.PlasmaBomb) => new( 255, 64, 0, 255 ),
		_ => base.SmokeTint
	};
	internal override GameMount EffectMount => ModelMount;
	internal override GameMount BlastMount => ModelMount;

	// This mod launches directly into powered flight.
	internal override void PrepareLaunch() { }

	internal void ReleaseRemoteControl() => _released = true;

	internal override void Guide()
	{
		if ( Age < _nextGuide ) return;
		_nextGuide += GuideInterval;
		if ( Guidance == RocketGuidance.RemoteControlled &&
			(!Launcher.IsValid() || Shooter?.GetComponent<PlayerController>() is not { Enabled: true }) )
			ReleaseRemoteControl();

		var forward = WorldRotation.Forward;
		var speed = Velocity.Length;
		var limit = 2000f;
		switch ( Guidance )
		{
			case RocketGuidance.LaserGuided:
				limit = 1500;
				Steer( FindSpotDirection(), speed );
				break;
			case RocketGuidance.Heatseeking:
				limit = 1200;
				Steer( FindHeatDirection( forward ), speed );
				break;
			case RocketGuidance.RemoteControlled:
				limit = 512;
				if ( !_released ) forward = Launcher.Owner.EyeTransform.Forward;
				WorldRotation = Rotation.LookAt( forward );
				Velocity = forward * (speed + Thrust);
				break;
			default:
				Velocity = forward * (speed + Thrust);
				break;
		}

		if ( Guidance != RocketGuidance.RemoteControlled )
			limit -= Warhead switch
			{
				RocketWarhead.Incendiary or RocketWarhead.PlasmaBomb => 200,
				RocketWarhead.Emp => 400,
				_ => 0
			};
		var underwater = WaterBody.Find( Scene, WorldPosition ).IsValid();
		if ( underwater ) limit = 300;
		if ( Velocity.Length > limit ) Velocity = Velocity.Normal * limit;
		if ( underwater ) ShowWaterTrail( WorldPosition - Velocity * GuideInterval, WorldPosition );
	}

	void Steer( Vector3 direction, float speed )
	{
		WorldRotation = Rotation.LookAt( direction );
		Velocity = Velocity * 0.8f + direction * System.MathF.Min( Thrust, speed * 0.4f );
	}

	Vector3 FindHeatDirection( Vector3 forward )
	{
		var direction = forward;
		var best = HeatRange;
		foreach ( var npc in Scene.GetAllComponents<Npc>() )
		{
			if ( npc.Health > 0 ) Consider( npc.GameObject, Npc.TargetEyePosition( npc.GameObject ) );
		}
		foreach ( var player in Scene.GetAllComponents<PlayerController>() )
		{
			if ( MapState.PlayerVitals( player ) is { Health: > 0 } )
				Consider( player.GameObject, player.EyePosition );
		}
		return direction;

		void Consider( GameObject candidate, Vector3 point )
		{
			if ( candidate == Shooter ) return;
			var offset = point - WorldPosition;
			if ( offset.LengthSquared > HeatRange * HeatRange ) return;
			var heading = offset.Normal;
			var dot = forward.Dot( heading );
			var score = offset.Length * (1 - dot);
			if ( dot <= 0.7f || score >= best ) return;
			var hit = Scene.Trace.Ray( WorldPosition, point ).IgnoreGameObjectHierarchy( GameObject )
				.WithoutTags( "trigger" ).Run();
			if ( hit.Hit && !hit.GameObject.IsDescendant( candidate ) && hit.GameObject != candidate ) return;
			best = score;
			direction = heading;
		}
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowWaterTrail( Vector3 start, Vector3 end )
		=> Blast.BubbleTrail( Scene, start, end, 4, "Rocket bubbles", ModelMount );

	internal override void OnImpact( IDamageable victim, Vector3 position, Vector3 normal )
	{
		switch ( Warhead )
		{
			case RocketWarhead.Incendiary:
			case RocketWarhead.PlasmaBomb:
				IncendiaryBlast.Detonate( this, position, normal, Warhead == RocketWarhead.PlasmaBomb );
				break;
			case RocketWarhead.Emp:
				LaserGrenadeField.Create( this, position + normal, emp: true );
				break;
			default:
				base.OnImpact( victim, position, normal );
				break;
		}
	}
}

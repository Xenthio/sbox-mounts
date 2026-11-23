using GoldSrc.HalfLife;

namespace GoldSrc.TeamFortressClassic;

[Title( "Flamethrower" ), Category( "Team Fortress Classic" )]
[WeaponPickup( "flame", "uranium", 200, EntityName = "tf_weapon_flamethrower", PickupModel = "w_egon", Bucket = 3, Weight = 20, Mount = "tfc" )]
public sealed class Flamethrower : ProjectileWeapon
{
	const float FireInterval = 0.15f;
	const float SubmergedInterval = 1;
	const float FlameSpeed = 600;
	SoundHandle _shotSound;
	bool Submerged => Owner.IsValid() && WaterBody.Find( Scene, Owner.EyePosition ).IsValid();

	internal override string WorldModelPath => "models/p_egon.mdl";
	internal override string ShotSequence => "fire1";
	internal override float? IdleAfterShot => Submerged ? SubmergedInterval : FireInterval;
	internal override (string Name, float? Delay) IdleAnimation =>
		Game.Random.Int( 0, 4 ) == 0 ? ("fidget1", 3) : ("idle1", 12.5f);

	internal override void Configure()
	{
		HoldType = WeaponHolds.Rifle;
		UsesClips = false;
		StartingAmmo = 50;
		PrimaryDelay = FireInterval;
	}

	public override Ray AimRay => Owner.IsValid() ? Owner.EyeTransform.ForwardRay : base.AimRay;
	public override bool CanSecondaryAttack() => false;

	public override void PrimaryAttack()
	{
		if ( Submerged ) SetNextPrimaryFire( SubmergedInterval );
		FireFlame( AimRay );
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void FireFlame( Ray ray ) => TryLaunch( ray, Release, extraDelay: Submerged ? SubmergedInterval - FireInterval : 0 );

	internal override bool Release( Ray ray )
	{
		var basis = Rotation.LookAt( ray.Forward );
		var origin = ray.Position + basis.Forward * 16 + basis.Right * 8 - basis.Up * 8;
		var submerged = Submerged;
		if ( !submerged )
		{
			var direction = (basis * Rotation.FromPitch( -30 )).Forward;
			NpcProjectile.Fire<FlamethrowerBurst>( Owner.GameObject, null, 15, null, origin, direction, FlameSpeed );
		}

		ShowFlame( origin, ray.Forward, submerged );
		return true;
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowFlame( Vector3 origin, Vector3 forward, bool submerged )
	{
		if ( Application.IsDedicatedServer || ContentMount is not { } mount ) return;
		if ( submerged )
		{
			Blast.BubbleTrail( Scene, origin, origin + forward * 128, 4, "TFC flamethrower bubbles", mount );
			return;
		}

		var puff = Blast.SpriteEffect( "TFC flamethrower puff", "fthrow", origin, 1, 16, mount: mount );
		if ( puff.IsValid() )
		{
			var flight = puff.AddComponent<FlamePuff>();
			flight.Velocity = forward * FlameSpeed;
			flight.Shooter = Owner?.GameObject;
		}
		_shotSound?.Stop();
		_shotSound = PlaySample( "weapons/flmfire2.wav", 0.9f );
	}

	protected override void OnDisabled()
	{
		_shotSound?.Stop();
		base.OnDisabled();
	}
}

[Hide]
public sealed class FlamethrowerBurst : NpcProjectile
{
	internal override float Lifetime => 1;
	internal override string HitTag => "burn";

	// The server traces an invisible burst. The client event draws the flame along the sight line.
	protected override void OnStart() { }

	internal override SceneTrace FlightTrace( Vector3 start, Vector3 end ) =>
		base.FlightTrace( start, end ).WithoutTags( "trigger", "playerclip", "playercontroller" );

	internal override void OnImpact( IDamageable victim, Vector3 position, Vector3 normal )
	{
		if ( victim is Component target && target.GetComponentInParent<PlayerController>() is { } player )
			BurningPlayer.Ignite( player, target, Shooter );
		base.OnImpact( victim, position, normal );
	}
}

[Hide]
public sealed class FlamePuff : Component
{
	internal Vector3 Velocity { get; set; }
	internal GameObject Shooter { get; set; }

	protected override void OnUpdate()
	{
		var start = WorldPosition;
		var end = start + Velocity * Time.Delta;
		var trace = Scene.Trace.Ray( start, end ).WithoutTags( "trigger", "playerclip", "playercontroller" ).UseHitboxes();
		if ( Shooter.IsValid() ) trace = trace.IgnoreGameObjectHierarchy( Shooter );
		if ( trace.Run().Hit ) GameObject.Destroy();
		else WorldPosition = end;
	}
}

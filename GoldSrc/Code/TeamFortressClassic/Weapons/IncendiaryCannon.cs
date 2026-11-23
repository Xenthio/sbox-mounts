using GoldSrc.HalfLife;

namespace GoldSrc.TeamFortressClassic;

[Title( "Incendiary Cannon" ), Category( "Team Fortress Classic" )]
[WeaponPickup( "rpg", "rockets", 50, EntityName = "tf_weapon_ic", PickupModel = "w_rpg", AlternateModel = "tfc_rpg", Bucket = 4, Weight = 20, Mount = "tfc" )]
public sealed class IncendiaryCannon : ProjectileWeapon
{
	internal override string HeldModelName => Ammo1 > 0 ? Definition.Model : Definition.AlternateModel;
	internal override string WorldModelPath => "models/p_rpg.mdl";
	internal override string DeploySequence => Ammo1 > 0 ? "draw1" : "draw2";
	internal override string ShotSequence => "fire";
	internal override string ShotSound => "weapons/sgun1.wav";
	internal override float? IdleAfterShot => 1.2f;
	internal override (string Name, float? Delay) IdleAnimation => Ammo1 > 0 ? ("fidget", 3) : ("idle2", 1);

	internal override void Configure()
	{
		HoldType = WeaponHolds.Shoulder;
		UsesClips = false;
		StartingAmmo = 50;
		PrimaryDelay = 1.2f;
	}

	public override Ray AimRay => Owner.IsValid() ? Owner.EyeTransform.ForwardRay : base.AimRay;
	public override bool CanSecondaryAttack() => false;

	internal override bool Release( Ray ray )
	{
		if ( ContentMount?.FindMountPath( "models/rpgrocket.mdl" ) is not { } path ) return false;
		var basis = Rotation.LookAt( ray.Forward );
		var origin = ray.Position + basis.Forward * 16 + basis.Right * 8 - basis.Up * 8;
		var direction = (basis * Rotation.FromPitch( -30 )).Forward;
		var rocket = NpcProjectile.Fire<IncendiaryRocket>( Owner.GameObject, $"mount://{Definition.Mount}/{path}.vmdl", 15, null, origin, direction, 600 );
		rocket.WorldRotation = basis;
		return true;
	}

	internal override void PlayLaunchSound()
	{
		PlaySample( ShotSound, 0.9f );
		if ( !IsProxy && Owner is { ThirdPerson: false } )
			MeleeViewPunch.Apply( Scene.Camera, new Angles( -5, 0, 0 ) );
	}

	public override void DryFire()
	{
		PlaySample( "weapons/357_cock1.wav", 0.8f );
		SetNextPrimaryFire( 0.2f );
	}
}

[Hide]
public sealed class IncendiaryRocket : NpcProjectile
{
	const float Radius = 180;
	const float RadiusThroughCover = 135;
	static GameMount Mount => Sandbox.Mounting.Directory.Get( "tfc" ) as GameMount;
	internal override float Lifetime => 5;
	internal override string HitTag => "burn";

	protected override void OnStart()
	{
		base.OnStart();
		if ( !Application.IsDedicatedServer && Mount is { } mount )
			ProjectileTrail.Attach( GameObject, mount, "Incendiary rocket trail", "smoke", 3, 5, new Color32( 224, 32, 32, 200 ) );
	}

	internal override SceneTrace FlightTrace( Vector3 start, Vector3 end ) =>
		base.FlightTrace( start, end ).WithoutTags( "trigger", "playerclip", "playercontroller" );

	internal override void HandleContact( SceneTraceResult hit )
	{
		var victim = hit.GameObject?.GetComponentInParent<IDamageable>();
		var contactDamage = Game.Random.Float( 10, 30 );
		if ( victim is Component direct )
			ApplyFireDamage( direct, contactDamage, hit.EndPosition, hit.EndPosition );

		var impact = hit.EndPosition;
		var origin = impact + hit.Normal * 32;
		var targets = Scene.FindInPhysics( new Sphere( origin, Radius ) )
			.Select( go => go.GetComponentInParent<IDamageable>() ).OfType<Component>().Distinct().ToArray();
		foreach ( var target in targets )
		{
			if ( !target.IsValid() ) continue;
			var center = Npc.TargetBodyPosition( target.GameObject );
			var trace = Scene.Trace.Ray( origin, center ).IgnoreGameObjectHierarchy( GameObject ).WithoutTags( "trigger", "debris" ).UseHitboxes().Run();
			if ( trace.Hit && trace.GameObject?.Root != target.GameObject.Root && center.Distance( origin ) > RadiusThroughCover ) continue;
			var self = target.GameObject.Root == Shooter?.Root;
			ApplyFireDamage( target, self ? Damage * 0.75f : Damage, origin, center );
		}

		Mount?.NpcAudio.Emit( $"weapons/explode{Game.Random.Int( 3, 5 )}.wav", origin, attenuation: 0.3f );
		ShowExplosion( origin, impact, hit.Normal, (int)(contactDamage * 0.5f) * 0.1f );
		GameObject.Destroy();
	}

	void ApplyFireDamage( Component target, float damage, Vector3 origin, Vector3 position )
	{
		if ( !target.IsValid() || target is not IDamageable victim ) return;
		if ( target.GetComponentInParent<PlayerController>() is { } player ) BurningPlayer.Ignite( player, target, Shooter );
		var info = new DamageInfo { Damage = damage, Attacker = Shooter, Weapon = GameObject, Origin = origin, Position = position };
		info.Tags.Add( "burn" );
		info.Tags.Add( "blast" );
		victim.OnDamage( info );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void ShowExplosion( Vector3 origin, Vector3 contact, Vector3 normal, float scale )
	{
		if ( Application.IsDedicatedServer || Mount is not { } mount ) return;
		ImpactDecal.Place( contact, normal, $"scorch{Game.Random.Int( 1, 2 )}", 40, mount );
		var sprite = WaterBody.Find( Game.ActiveScene, origin ).IsValid() ? "WXplo1" : "explode01";
		Blast.SpriteEffect( "TFC incendiary explosion", sprite, origin, scale, 15, mount: mount );
	}
}

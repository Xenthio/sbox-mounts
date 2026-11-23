using GoldSrc.HalfLife;

namespace GoldSrc.TeamFortressClassic;

[Title( "Rocket Launcher" ), Category( "Team Fortress Classic" )]
[WeaponPickup( "tfc_rpg", "rockets", 50, EntityName = "tf_weapon_rpg", PickupModel = "p_rpg", Bucket = 4, Weight = 20, Mount = "tfc" )]
public sealed class RocketLauncher : ProjectileWeapon
{
	internal override string WorldModelPath => "models/p_rpg.mdl";
	internal override string DeploySequence => "draw1";
	internal override string ShotSequence => "fire";
	internal override string ShotSound => "weapons/rocketfire1.wav";
	internal override string ReloadSequence => "rel_start";
	internal override float IdleAfterReloadStart => ReloadStartTime;
	internal override float? IdleAfterShot => 0.8f;
	internal override (string Name, float? Delay) IdleAnimation => (Clip1 == 0 ? "idle2" : "idle", null);

	internal override void Configure()
	{
		HoldType = WeaponHolds.Shoulder;
		ClipMaxSize = 4;
		PrimaryAutomatic = true;
		PrimaryDelay = 0.8f;
		IncrementalReloading = true;
		ReloadStartTime = 1.35f;
		ReloadTime = 1.25f;
	}

	public override Ray AimRay => Owner.IsValid() ? Owner.EyeTransform.ForwardRay : base.AimRay;

	public override bool CanSecondaryAttack() => false;
	public override bool CanReload() => NextPrimaryFire <= 0 && base.CanReload();

	internal override bool Release( Ray ray )
	{
		if ( ContentMount?.FindFile( "models/rpgrocket.mdl" ) is not { } model ) return false;
		var basis = Rotation.LookAt( ray.Forward );
		var origin = Owner.EyePosition + basis.Forward * 16 + basis.Right * 8 - basis.Up * 8;
		NpcProjectile.Fire<FortressRocket>( Owner.GameObject, $"mount://tfc/{model}.vmdl", 92, null, origin, ray.Forward, 900 );
		return true;
	}

	protected override void OnReloadInserted()
	{
		base.OnReloadInserted();
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( "rel_cycle" );
	}

	protected override void OnReloadFinished()
	{
		base.OnReloadFinished();
		if ( IsActive ) ViewModel?.GetComponent<StudioWeaponView>()?.Play( "rel_end" );
	}
}

[Hide]
public sealed class FortressRocket : RocketProjectile
{
	internal override float IgnitionDelay => 0;
	internal override float SmokeLifetime => 4;
	internal override float Lifetime => 5;
	internal override GameMount EffectMount => Sandbox.Mounting.Directory.Get( "tfc" ) as GameMount;

	internal override SceneTrace FlightTrace( Vector3 start, Vector3 end ) =>
		base.FlightTrace( start, end ).WithoutTags( "trigger", "playerclip", "playercontroller" );

	internal override void OnImpact( IDamageable victim, Vector3 position, Vector3 normal )
	{
		var damage = Damage + Game.Random.Float( 0, 20 );
		var origin = position + normal * ((damage - 50) * 0.6f);
		var submerged = WaterBody.Find( Scene, origin ).IsValid();
		var victims = Scene.FindInPhysics( new Sphere( origin, damage ) )
			.Select( go => go.GetComponentInParent<IDamageable>() ).OfType<Component>().Distinct().ToArray();
		foreach ( var target in victims )
		{
			if ( !target.IsValid() || !Npc.SharesWaterLevel( target.GameObject, submerged ) ) continue;
			var center = Npc.TargetBodyPosition( target.GameObject );
			var trace = Scene.Trace.Ray( origin, center ).IgnoreGameObjectHierarchy( GameObject ).WithoutTags( "trigger", "debris" ).UseHitboxes().Run();
			if ( trace.Hit && trace.GameObject?.Root != target.GameObject.Root ) continue;
			var self = target.GameObject.Root == Shooter?.Root;
			var amount = (damage - origin.Distance( trace.EndPosition ) * 0.5f) * (self ? 0.75f : 1);
			if ( amount <= 0 ) continue;
			var info = new DamageInfo { Damage = amount, Origin = origin, Position = trace.EndPosition, Attacker = Shooter, Weapon = GameObject };
			info.Tags.Add( "blast" );
			((IDamageable)target).OnDamage( info );
			if ( target.IsValid() ) Npc.PushVictim( target, (center - origin).Normal * amount * 5 );
		}

		EffectMount?.NpcAudio.Emit( $"weapons/explode{Game.Random.Int( 3, 5 )}.wav", origin, attenuation: 0.3f );
		ShowExplosion( origin );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void ShowExplosion( Vector3 position )
	{
		var mount = Sandbox.Mounting.Directory.Get( "tfc" ) as GameMount;
		if ( mount is not null ) Blast.SpriteEffect( "TFC rocket explosion", "explode01", position, 1, 15, mount: mount );
	}
}

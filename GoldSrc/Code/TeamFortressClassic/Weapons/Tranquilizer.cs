using GoldSrc.HalfLife;

namespace GoldSrc.TeamFortressClassic;

[Title( "Tranquilizer Gun" ), Category( "Team Fortress Classic" )]
[WeaponPickup( "tfc_pistol", "buckshot", 200, EntityName = "tf_weapon_tranq", PickupModel = "p_spygun", Bucket = 1, Weight = 10, Mount = "tfc" )]
public sealed class Tranquilizer : ProjectileWeapon
{
	internal override string WorldModelPath => "models/p_spygun.mdl";
	internal override string ShotSequence => "shoot";
	internal override float? IdleAfterShot => 12.5f;
	internal override (string Name, float? Delay) IdleAnimation => Game.Random.Int( 0, 3 ) switch
	{
		0 or 3 => ("idle3", 3.0625f),
		1 => ("idle1", 3.75f),
		_ => ("idle2", 2.5f)
	};

	public override Ray AimRay => Owner.IsValid() ? Owner.EyeTransform.ForwardRay : base.AimRay;

	internal override void Configure()
	{
		UsesClips = false;
		StartingAmmo = 17;
		PrimaryDelay = 1.5f;
		HoldType = WeaponHolds.Pistol;
		Handedness = WeaponHandedness.Right;
	}

	protected override void CreateViewModel()
	{
		base.CreateViewModel();
		ViewModel?.GetComponent<SkinnedModelRenderer>()?.SetBodyGroup( "silencer", 1 );
	}

	internal override bool Release( Ray ray )
	{
		var model = ContentMount?.FindMountPath( "models/nail.mdl" );
		if ( model is null ) return false;
		NpcProjectile.Fire<TranquilizerDart>( Owner.GameObject, $"mount://{Definition.Mount}/{model}.vmdl", 20, null, ray.Position, ray.Forward, 1500 );
		return true;
	}

	public override bool CanSecondaryAttack() => false;

	public override void DryFire()
	{
		PlaySample( "weapons/357_cock1.wav", 0.8f );
		SetNextPrimaryFire( 0.2f );
	}

	internal override void PlayLaunchSound()
	{
		PlaySample( "weapons/dartgun.wav", 0.9f );
		if ( !IsProxy && Owner is { ThirdPerson: false } )
			MeleeViewPunch.Apply( Scene.Camera, new Angles( -2, 0, 0 ) );
	}
}

[Hide]
public sealed class TranquilizerDart : Nail
{
	internal override SceneTrace FlightTrace( Vector3 start, Vector3 end ) =>
		base.FlightTrace( start, end ).WithoutTags( "playercontroller" );

	internal override void HandleContact( SceneTraceResult hit )
	{
		if ( hit.GameObject?.GetComponentInParent<PlayerController>() is { } player )
			Tranquilized.Apply( player );
		else if ( hit.GameObject?.GetComponentInParent<IDamageable>() is null && hit.Collider is { Static: true } )
			ShowImpact( hit.EndPosition, hit.Normal, Game.Random.Int( 1, 5 ) );
		base.HandleContact( hit );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowImpact( Vector3 position, Vector3 normal, int variant )
	{
		if ( !Application.IsDedicatedServer && Sandbox.Mounting.Directory.Get( "tfc" ) is GameMount mount )
			ImpactDecal.Place( position, normal, $"shot{variant}", 10, mount );
	}
}

using GoldSrc.HalfLife;

namespace GoldSrc.SvenCoop;

[Title( "Sven Snarks" ), Category( "Sven Co-op" )]
[WeaponPickup( "squeak", "snarks", 15, ClassName = "snark", PickupModel = "w_sqknest", Bucket = 4, Weight = 5, Mount = "svencoop" )]
public sealed class SnarkWeapon : ClassicSnarkWeapon
{
	const int ChumtoadCost = 10;

	[ConVar( "svencoop_chumtoad_throw", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting,
		Help = "Allow Sven Snarks to throw a personal Chumtoad for ten rounds of reserve ammo." )]
	public static bool ChumtoadThrowEnabled { get; set; }

	internal override void Configure()
	{
		base.Configure();
		SecondaryDelay = 1;
	}

	public override bool CanSecondaryAttack() => ChumtoadThrowEnabled && Ammo1 >= ChumtoadCost && base.CanSecondaryAttack();
	public override void SecondaryAttack() => ThrowChumtoad( AimRay );

	[Rpc.Host( NetFlags.OwnerOnly )]
	void ThrowChumtoad( Ray ray )
	{
		if ( !ChumtoadThrowEnabled ) return;
		TryLaunch( ray, ReleaseChumtoad, secondary: true, ammoCost: ChumtoadCost, primaryCooldown: PrimaryDelay );
	}

	internal override bool TryAimThrow( Ray ray, out Vector3 position, out Vector3 forward )
	{
		forward = ray.Forward;
		return TraceThrow( forward, 20, 64, 0.25f, out position );
	}

	bool TraceThrow( Vector3 forward, float start, float end, float minimumClearance, out Vector3 hitPosition )
	{
		// Sven raises the crouched throw to the standing hull centre too.
		var origin = Owner.WorldPosition + Vector3.Up * 36;
		var trace = Scene.Trace.Ray( origin + forward * start, origin + forward * end )
			.IgnoreGameObjectHierarchy( Owner.GameObject ).WithoutTags( "trigger" ).Run();
		hitPosition = trace.EndPosition;
		return !trace.StartedSolid && trace.Fraction > minimumClearance;
	}

	bool ReleaseChumtoad( Ray ray )
	{
		var prefab = GameObject.GetPrefab( Chumtoad.Prefab );
		var forward = ray.Forward;
		if ( !prefab.IsValid() || !TraceThrow( forward, 48, 96, 0.75f, out _ ) ) return false;

		var previous = Scene.GetAllComponents<Chumtoad>().FirstOrDefault( toad => toad.PetOwner == Owner.GameObject && toad.Health > 0 );
		var position = Owner.WorldPosition + Vector3.Up * 36 + forward * 64;
		var rotation = Rotation.FromYaw( Rotation.LookAt( forward ).Yaw() );
		var pet = prefab.Clone( new Transform( position, rotation ), startEnabled: false ).GetComponent<Chumtoad>( true );
		pet.PetOwner = Owner.GameObject;
		pet.GameObject.NetworkSpawn( true, null );
		pet.Launch( Owner.Velocity + forward * 200 );
		previous?.Retire();
		QueueRedraw();
		return true;
	}

	internal override void PlayLaunchSound()
	{
		if ( !IsProxy ) base.PlayLaunchSound();
	}
}

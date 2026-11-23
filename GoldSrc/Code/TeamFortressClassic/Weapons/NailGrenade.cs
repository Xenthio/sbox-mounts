using GoldSrc.HalfLife;

namespace GoldSrc.TeamFortressClassic;

[Title( "Nail Grenade" ), Category( "Team Fortress Classic" )]
[WeaponPickup( "grenade", "tfc_nail_grenades", 2, EntityName = "tf_weapon_nailgrenade",
	PickupModel = "ngrenade", Bucket = 4, Mount = "tfc" )]
public sealed class NailGrenade : FortressHandGrenadeWeapon
{
	internal override void Configure()
	{
		base.Configure();
		StartingAmmo = 1;
	}

	internal override string LaunchGrenade( Vector3 origin, Vector3 direction, float pitch, float pinAge ) =>
		Launch<FortressNailGrenade>( origin, pinAge, "TFC nail grenade", "models/ngrenade.mdl" );
}

[Hide]
public sealed class FortressNailGrenade : FortressExplosiveGrenade
{
	const float Rise = 32;
	const float VolleyDelay = 0.1f;
	const int VolleyCount = 41;
	const int NailsPerVolley = 4;
	const float NailDamage = 18;
	const float NailSpeed = 1000;

	bool _deployed;
	int _volleysRemaining;
	float _yaw;
	TimeUntil _nextVolley;
	string _nailModel;

	internal override void OnFuseExpired()
	{
		if ( Mount?.FindMountPath( "models/nail.mdl" ) is not { } model )
		{
			base.OnFuseExpired();
			return;
		}

		var lift = Scene.Trace.Ray( WorldPosition, WorldPosition + Vector3.Up * Rise )
			.IgnoreGameObjectHierarchy( GameObject ).IgnoreGameObjectHierarchy( Thrower )
			.WithoutTags( "trigger", "debris" ).Run();
		GetComponent<ModelCollider>().Enabled = false;
		GetComponent<Rigidbody>().Enabled = false;
		WorldPosition = lift.EndPosition;
		WorldRotation = Rotation.Identity;
		_nailModel = $"mount://tfc/{model}.vmdl";
		_volleysRemaining = VolleyCount;
		_nextVolley = 0.5f;
		_deployed = true;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;
		if ( !_deployed )
		{
			base.OnFixedUpdate();
			return;
		}
		if ( _nextVolley > 0 ) return;
		if ( _volleysRemaining == 0 )
		{
			base.OnFuseExpired();
			return;
		}

		var turn = Game.Random.Float( 30, 40 );
		for ( var i = 0; i < NailsPerVolley; i++ )
		{
			_yaw = (_yaw + turn) % 360;
			var direction = Rotation.FromYaw( _yaw ).Forward;
			var origin = WorldPosition + direction * 12;
			NpcProjectile.Fire<GrenadeNail>( Thrower, _nailModel, NailDamage, null, origin, direction, NailSpeed );
			if ( Game.Random.Int( 0, 5 ) == 0 )
			{
				var sample = Game.Random.Int( 0, 1 ) == 0 ? "weapons/airgun_1.wav" : "weapons/spike2.wav";
				Mount?.NpcAudio.Emit( sample, origin, pitch: Game.Random.Int( 94, 109 ) / 100f, attenuation: 0.8f );
			}
		}
		WorldRotation = Rotation.FromYaw( _yaw );
		_volleysRemaining--;
		_nextVolley = VolleyDelay;
	}
}

[Hide]
public sealed class GrenadeNail : Nail
{
	// The grenade fires the nail, while the player who threw it gets damage credit.
	internal override GameObject CollisionOwner => null;
}

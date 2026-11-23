using GoldSrc.HalfLife;

namespace GoldSrc.TeamFortressClassic;

[Title( "Caltrop Canister" ), Category( "Team Fortress Classic" )]
[WeaponPickup( "grenade", "tfc_caltrop_canisters", 3, EntityName = "tf_weapon_caltropgrenade",
	PickupModel = "caltrop", HudIcon = "d_caltrop", Bucket = 4, Mount = "tfc" )]
public sealed class CaltropCanister : FortressHandGrenadeWeapon
{
	internal override float HeldFuse => MinimumPinTime + 0.5f;

	internal override void Configure()
	{
		base.Configure();
		StartingAmmo = 2;
	}

	internal override string LaunchGrenade( Vector3 origin, Vector3 direction, float pitch, float pinAge )
	{
		if ( ContentMount?.FindMountPath( "models/caltrop.mdl" ) is not { } path ) return "throw1";
		var model = Model.Load( $"mount://tfc/{path}.vmdl" );
		for ( var i = 0; i < 6; i++ )
			FortressCaltrop.Scatter( Owner.GameObject, model, origin );
		return "throw1";
	}
}

[Hide]
public sealed class FortressCaltrop : Component
{
	static readonly BBox Hull = new( new Vector3( -4, -4, -8 ), new Vector3( 4, 4, 4 ) );
	GameObject _thrower;
	ModelRenderer _renderer;
	BoxCollider _trigger;
	Vector3 _velocity;
	Angles _spin;
	TimeUntil _expires, _nextGroundCheck;
	bool _settled, _spent;

	internal static void Scatter( GameObject thrower, Model model, Vector3 position )
	{
		var go = new GameObject( false, "TFC caltrop" ) { WorldPosition = position };
		var trigger = go.AddComponent<BoxCollider>();
		trigger.Center = Hull.Center;
		trigger.Scale = Hull.Size;
		trigger.IsTrigger = true;
		trigger.Enabled = false;
		var display = new GameObject( go, true, "Caltrop model" );
		display.LocalRotation = Rotation.FromPitch( Game.Random.Float( 0, 360 ) );
		var renderer = display.AddComponent<ModelRenderer>();
		renderer.Model = model;

		var caltrop = go.AddComponent<FortressCaltrop>();
		caltrop._thrower = thrower;
		caltrop._renderer = renderer;
		caltrop._trigger = trigger;
		caltrop._velocity = new Vector3( Game.Random.Float( -150, 150 ), Game.Random.Float( -150, 150 ), Game.Random.Float( 150, 200 ) );
		caltrop._spin = new Angles( Game.Random.Float( -400, 400 ), Game.Random.Float( -400, 400 ), Game.Random.Float( -400, 400 ) );
		caltrop._expires = Game.Random.Float( 10, 15 );
		caltrop._nextGroundCheck = 0;
		go.NetworkSpawn( true, null );
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost || _spent ) return;
		if ( _expires <= 0 )
		{
			GameObject.Destroy();
			return;
		}
		if ( !_settled ) Fly();
		if ( !_trigger.Enabled && _nextGroundCheck <= 0 )
		{
			_nextGroundCheck = 0.2f;
			if ( _settled )
			{
				_renderer.LocalRotation = Rotation.From( 90, 90, 90 );
				_trigger.Enabled = true;
			}
		}
		if ( !_trigger.Enabled ) return;

		foreach ( var collider in _trigger.Touching )
		{
			if ( !collider.IsValid() || collider.GetComponentInParent<PlayerController>() is not { IsOnGround: true } player ) continue;
			if ( Maps.MapState.PlayerVitals( player ) is not { Health: > 0 } vitals ) continue;
			StepOn( player, vitals );
			break;
		}
	}

	void Fly()
	{
		var step = Time.Delta;
		var gravity = Scene.PhysicsWorld.Gravity * 0.5f;
		var end = WorldPosition + (_velocity + gravity * (step * 0.5f)) * step;
		var trace = Scene.Trace.Box( Hull, WorldPosition, end ).IgnoreGameObjectHierarchy( GameObject )
			.IgnoreGameObjectHierarchy( _thrower ).WithoutTags( "trigger", "player", "playercontroller", "npc", "debris" ).Run();
		_velocity += gravity * step;
		_renderer.LocalRotation *= (_spin * step).ToRotation();
		WorldPosition = trace.EndPosition;
		if ( !trace.Hit ) return;
		WorldPosition += trace.Normal * 0.1f;
		_settled = trace.Normal.z > 0.7f;
		_velocity -= trace.Normal * System.MathF.Min( 0, _velocity.Dot( trace.Normal ) );
	}

	void StepOn( PlayerController player, IDamageable vitals )
	{
		_spent = true;
		LegWound.Wound( player, vitals );
		var info = new DamageInfo { Damage = 10, Attacker = _thrower, Weapon = GameObject, Origin = WorldPosition, Position = WorldPosition };
		info.Tags.Add( "caltrop" );
		vitals.OnDamage( info );
		GameObject.Destroy();
	}
}

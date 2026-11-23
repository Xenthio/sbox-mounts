using GoldSrc.HalfLife;

namespace GoldSrc.TeamFortressClassic;

[Title( "Napalm Grenade" ), Category( "Team Fortress Classic" )]
[WeaponPickup( "grenade", "tfc_napalm_grenades", 4, EntityName = "tf_weapon_napalmgrenade",
	PickupModel = "napalm", HudIcon = "d_napalmgrenade", Bucket = 4, Mount = "tfc" )]
public sealed class NapalmGrenade : FortressHandGrenadeWeapon
{
	internal override void Configure()
	{
		base.Configure();
		StartingAmmo = 4;
	}

	internal override string LaunchGrenade( Vector3 origin, Vector3 direction, float pitch, float pinAge ) =>
		Launch<FortressNapalmGrenade>( origin, pinAge, "TFC napalm grenade", "models/napalm.mdl" );
}

[Hide]
public sealed class FortressNapalmGrenade : FortressHandGrenade
{
	const int BurnPulses = 7;
	const float Damage = 20;
	const float Radius = 180;
	int _pulsesRemaining;
	TimeUntil _nextDamage, _nextFlames;

	internal override void OnFuseExpired()
	{
		var floor = TraceFloor();
		if ( floor.Hit ) WorldPosition = floor.EndPosition + floor.Normal * (Damage * 0.6f);
		GetComponent<ModelRenderer>().Enabled = false;
		GetComponent<ModelCollider>().Enabled = false;
		GetComponent<Rigidbody>().Enabled = false;
		_pulsesRemaining = BurnPulses;
		_nextDamage = _nextFlames = 0;
		Mount?.NpcAudio.Emit( $"weapons/explode{Game.Random.Int( 3, 5 )}.wav", WorldPosition );
		Mount?.NpcAudio.Emit( "weapons/debris", WorldPosition, volume: 0.55f, attenuation: 0.8f );
		ShowIgnition( WorldPosition );
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;
		if ( _pulsesRemaining == 0 )
		{
			base.OnFixedUpdate();
			return;
		}
		if ( _nextDamage <= 0 )
		{
			Burn();
			_nextDamage = 1;
			if ( --_pulsesRemaining == 0 )
			{
				GameObject.Destroy();
				return;
			}
		}
		if ( _nextFlames > 0 ) return;
		_nextFlames = 0.5f;
		ShowFlames( WorldPosition );
	}

	void Burn()
	{
		var origin = WorldPosition + Vector3.Up;
		var thrower = Thrower?.GetComponent<PlayerController>();
		// A player standing on the ignition point must not shield the entire field.
		var insideThrower = thrower.IsValid() && thrower.BodyBox().Contains( origin - thrower.WorldPosition );
		Mount?.NpcAudio.Emit( "ambience/fire1.wav", WorldPosition, attenuation: 0.8f );
		var victims = Scene.FindInPhysics( new Sphere( origin, Radius ) )
			.Select( go => go.GetComponentInParent<IDamageable>() ).OfType<Component>().Distinct().ToArray();
		foreach ( var target in victims )
		{
			if ( !target.IsValid() ) continue;
			var center = Npc.TargetBodyPosition( target.GameObject );
			if ( origin.DistanceSquared( center ) > Radius * Radius ) continue;
			var ray = Scene.Trace.Ray( origin, center ).IgnoreGameObjectHierarchy( GameObject )
				.WithoutTags( "trigger", "debris" ).UseHitboxes();
			if ( insideThrower ) ray = ray.IgnoreGameObjectHierarchy( Thrower );
			var trace = ray.Run();
			if ( trace.Hit && trace.GameObject?.Root != target.GameObject.Root ) continue;
			if ( target.GetComponentInParent<PlayerController>() is { } player ) BurningPlayer.Ignite( player, target, Thrower );
			var amount = target.GameObject.Root == Thrower?.Root ? Damage * 0.75f : Damage;
			var info = new DamageInfo { Damage = amount, Origin = origin, Position = trace.EndPosition, Attacker = Thrower, Weapon = GameObject };
			info.Tags.Add( "burn" );
			info.Tags.Add( "blast" );
			((IDamageable)target).OnDamage( info );
		}
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void ShowIgnition( Vector3 position )
	{
		if ( Mount is { } mount ) Blast.SpriteEffect( "TFC napalm ignition", "explode01", position, 3, 15, additive: false, mount: mount );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void ShowFlames( Vector3 position )
	{
		if ( Mount is not { } mount ) return;
		for ( var i = 0; i < 12; i++ )
		{
			var offset = new Vector3( Game.Random.Float( -100, 100 ), Game.Random.Float( -100, 100 ), 0 );
			var flame = Blast.SpriteEffect( "TFC napalm flame", "playerflame", position + offset, 1, 15, mount: mount );
			if ( flame.IsValid() ) flame.GetComponent<TemporaryEffect>().DestroyAfterSeconds = 0.8f;
		}
	}
}

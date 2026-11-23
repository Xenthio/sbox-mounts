using GoldSrc.HalfLife;

namespace GoldSrc.TeamFortressClassic;

[Title( "Gas Grenade" ), Category( "Team Fortress Classic" )]
[WeaponPickup( "grenade", "tfc_gas_grenades", 2, EntityName = "tf_weapon_gasgrenade",
	PickupModel = "spy_grenade", HudIcon = "d_gasgrenade", Bucket = 4, Mount = "tfc" )]
public sealed class GasGrenade : FortressHandGrenadeWeapon
{
	internal override void Configure()
	{
		base.Configure();
		StartingAmmo = 2;
	}

	internal override string LaunchGrenade( Vector3 origin, Vector3 direction, float pitch, float pinAge ) =>
		Launch<FortressGasGrenade>( origin, pinAge, "TFC gas grenade", "models/spy_grenade.mdl" );
}

[Hide]
public sealed class FortressGasGrenade : FortressHandGrenade
{
	const int Pulses = 20;
	const float Radius = 240;
	const float Damage = 10;
	int _pulsesRemaining;
	TimeUntil _nextPulse;

	internal override void OnFuseExpired()
	{
		_pulsesRemaining = Pulses;
		_nextPulse = 0.1f;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;
		if ( _pulsesRemaining == 0 )
		{
			base.OnFixedUpdate();
			return;
		}
		if ( _nextPulse > 0 ) return;

		if ( _pulsesRemaining % 2 == 0 ) ShowCloud( WorldPosition );
		ExposePlayers();
		_nextPulse = 0.75f;
		if ( --_pulsesRemaining == 0 ) GameObject.Destroy();
	}

	void ExposePlayers()
	{
		foreach ( var player in Scene.GetAllComponents<PlayerController>().ToArray() )
		{
			if ( !player.IsValid() || !player.Enabled ) continue;
			var position = FortressHandGrenadeWeapon.HullCenter( player );
			if ( position.DistanceSquared( WorldPosition ) > Radius * Radius ) continue;
			var vitals = Maps.MapState.PlayerVitals( player );
			if ( vitals is { Health: <= 0 } ) continue;

			var info = new DamageInfo { Damage = Damage, Origin = WorldPosition, Position = position, Attacker = Thrower, Weapon = GameObject };
			info.Tags.Add( "poison" );
			vitals?.OnDamage( info );
			if ( player.IsValid() && vitals is not { Health: <= 0 } ) GasHallucination.Expose( player );
		}
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void ShowCloud( Vector3 position )
	{
		if ( Application.IsDedicatedServer || Mount is not { } mount ) return;
		const float lifetime = 2;
		const int particles = 160;
		var go = new GameObject( false, "TFC gas cloud" ) { NetworkMode = NetworkMode.Never, WorldPosition = position };
		var effect = go.AddComponent<ParticleEffect>();
		effect.MaxParticles = particles;
		effect.Lifetime = lifetime;
		effect.ApplyShape = true;
		effect.InitialScale = 4;
		go.AddComponent<ParticleSpriteRenderer>().Sprite = PaletteParticle.Sprite;
		go.AddComponent<TemporaryEffect>().DestroyAfterSeconds = lifetime;
		go.Enabled = true;

		for ( var i = 0; i < particles; i++ )
		{
			var direction = Vector3.Random.Normal;
			var offset = direction * Game.Random.Float( 0, Radius );
			if ( effect.Emit( position + offset, 0 ) is not { } particle ) continue;
			particle.Color = mount.PaletteColor( 195 + (i & 7) );
			particle.Velocity = direction * Game.Random.Float( 10, 45 ) + Vector3.Up * 12;
		}
	}
}

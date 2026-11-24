using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

[Hide]
public abstract class BurningRifleGrenade : ContactGrenade
{
	internal virtual bool Plasma => false;

	internal override void OnImpact( IDamageable victim, Vector3 position, Vector3 normal ) =>
		IncendiaryBlast.Detonate( this, position, normal, Plasma );
}

internal static class IncendiaryBlast
{
	internal static void Detonate( NpcProjectile projectile, Vector3 position, Vector3 normal, bool plasma ) =>
		Detonate( projectile.GameObject, projectile.Shooter, position, normal, projectile.Damage, projectile.ModelMount, plasma );

	internal static void Detonate( GameObject charge, GameObject attacker, Vector3 position, Vector3 normal, float damage, GameMount mount, bool plasma )
	{
		if ( WaterBody.Find( charge.Scene, position ).IsValid() )
		{
			Blast.Detonate( charge, attacker, position, normal, damage * 0.5f, true, mount );
			return;
		}

		var origin = position + normal * ((damage - 24) * 0.6f);
		Blast.DamageInRadius( charge, attacker, origin, damage, damage * 2.5f, "burn", "nevergib" );
		ShowBlast( mount?.Ident, origin, position, normal, damage, plasma, plasma ? [] : FlamePositions( charge, position + normal * 64 ) );
	}

	static readonly Vector2[] FlameOffsets =
	[
		new( -64, 128 ), new( 0, 128 ), new( 64, 128 ),
		new( -128, 64 ), new( -64, 64 ), new( 0, 64 ), new( 64, 64 ), new( 128, 64 ),
		new( -128, 0 ), new( -64, 0 ), Vector2.Zero, new( 64, 0 ), new( 128, 0 ),
		new( -128, -64 ), new( -64, -64 ), new( 0, -64 ), new( 64, -64 ), new( 128, -64 ),
		new( -64, -128 ), new( 0, -128 ), new( 64, -128 )
	];

	static Vector3[] FlamePositions( GameObject charge, Vector3 origin )
	{
		var scene = charge.Scene;
		var positions = new List<Vector3>();
		foreach ( var offset in FlameOffsets )
		{
			var point = origin + new Vector3( offset.x, offset.y, 0 );
			var hit = scene.Trace.Ray( origin, point ).IgnoreGameObjectHierarchy( charge )
				.WithoutTags( "npc", "player", "playercontroller", "trigger" ).Run();
			if ( !hit.Hit && !hit.StartedSolid && !WaterBody.Find( scene, point ).IsValid() )
				positions.Add( point );
		}
		return positions.ToArray();
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static async void ShowBlast( string ident, Vector3 origin, Vector3 contact, Vector3 normal, float damage, bool plasma, Vector3[] flames )
	{
		if ( Application.IsDedicatedServer || ident is null || Sandbox.Mounting.Directory.Get( ident ) is not GameMount mount ) return;
		var scene = Game.ActiveScene;
		if ( plasma ) RifleGrenadeSphere.Show( contact, new Color32( 255, 64, 0, 255 ), damage * 2, damage * 2, 0, 0.5f, mount );

		Blast.SpriteEffect( "Rifle grenade explosion", "fexplo", origin, ((damage - 50) * 0.06f).Clamp( 1, 25.5f ), 15, mount: mount );
		if ( !normal.IsNearlyZero() )
			ImpactDecal.Place( contact, normal, $"scorch{Game.Random.Int( 1, 2 )}", 48, mount );
		NpcAudio.Play( mount.NpcAudio.Find( $"weapons/explode{Game.Random.Int( 3, 5 )}.wav" ), origin, attenuation: 0.3f );
		NpcAudio.Play( mount.NpcAudio.Find( $"weapons/debris{Game.Random.Int( 1, 3 )}.wav" ), origin );
		foreach ( var point in flames )
			Blast.SpriteEffect( "Incendiary grenade flame", "fireball", point, 1, 10, mount: mount );

		await GameTask.DelaySeconds( 0.3f );
		if ( scene.IsValid() ) Blast.SmokePuff( "Rifle grenade smoke", origin, (damage - 50) * 0.08f, mount );
	}
}

[Hide]
public sealed class IncendiaryRifleGrenade : BurningRifleGrenade;

[Hide]
public sealed class PlasmaRifleGrenade : BurningRifleGrenade
{
	internal override bool Plasma => true;
}

[Hide]
public sealed class LaserRifleGrenade : ContactGrenade
{
	internal override void OnImpact( IDamageable victim, Vector3 position, Vector3 normal ) =>
		LaserGrenadeField.Create( this, position + normal, emp: false );
}

[Hide]
public sealed class LaserGrenadeField : Component
{
	GameObject _attacker;
	float _damage, _fullDamageTime;
	string[] _damageTags;

	const float Tick = 0.1f;
	const float FadeRate = 0.3f;
	TimeSince _age;
	TimeUntil _nextTick = Tick;
	float _radius = 1;

	internal static void Create( NpcProjectile projectile, Vector3 origin, bool emp ) =>
		Create( projectile.Shooter, origin, projectile.Damage, projectile.ModelMount, emp,
			emp ? 2 : 1, emp ? "EMP rocket field" : "Laser grenade field" );

	internal static void Create( GameObject attacker, Vector3 origin, float damage, GameMount mount, bool emp, float fullDamageTime, string name )
	{
		var field = new GameObject( false, name ) { WorldPosition = origin };
		field.NetworkMode = NetworkMode.Never;
		var effect = field.AddComponent<LaserGrenadeField>();
		effect._attacker = attacker;
		effect._damage = damage;
		effect._fullDamageTime = fullDamageTime;
		effect._damageTags = emp ? ["shock", "nevergib"] : ["energybeam"];
		field.Enabled = true;

		ShowField( mount?.Ident, origin, damage, fullDamageTime, emp );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void ShowField( string ident, Vector3 origin, float damage, float fullDamageTime, bool emp )
	{
		if ( Application.IsDedicatedServer || ident is null || Sandbox.Mounting.Directory.Get( ident ) is not GameMount mount ) return;
		var color = emp ? new Color32( 128, 192, 255, 255 ) : new Color32( 255, 96, 96, 255 );
		RifleGrenadeSphere.Show( origin, color, damage * 2, damage * 2.5f, fullDamageTime, FadeRate, mount );
		NpcAudio.Play( mount.NpcAudio.Find( "doors/aliendoor1.wav" ), origin );
	}

	protected override void OnStart() => _age = 0;

	protected override void OnFixedUpdate()
	{
		if ( _nextTick > 0 ) return;
		_nextTick += Tick;
		var strength = 1 - System.MathF.Max( _age - _fullDamageTime, 0 ) * FadeRate;
		if ( strength <= 0 )
		{
			GameObject.Destroy();
			return;
		}

		_radius = System.MathF.Min( _radius + _damage * 0.25f, _damage * 2 );
		Blast.ApplyRadiusDamage( GameObject, _attacker, WorldPosition, _damage * 0.25f * 0.75f * strength,
			_radius, ignoreAttacker: false, _damageTags );
	}
}

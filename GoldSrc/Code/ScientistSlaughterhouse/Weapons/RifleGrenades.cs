using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

[Hide]
public abstract class BurningRifleGrenade : ContactGrenade
{
	internal virtual bool Plasma => false;

	internal override void OnImpact( IDamageable victim, Vector3 position, Vector3 normal )
	{
		if ( WaterBody.Find( Scene, position ).IsValid() )
		{
			Blast.Detonate( GameObject, Shooter, position, normal, Damage * 0.5f, true, ModelMount );
			return;
		}

		var origin = position + normal * ((Damage - 24) * 0.6f);
		Blast.DamageInRadius( GameObject, Shooter, origin, Damage, Damage * 2.5f, "burn", "nevergib" );
		ShowBlast( origin, position, normal, Damage, Plasma ? [] : FlamePositions( position + normal * 64 ) );
	}

	static readonly Vector2[] FlameOffsets =
	[
		new( -64, 128 ), new( 0, 128 ), new( 64, 128 ),
		new( -128, 64 ), new( -64, 64 ), new( 0, 64 ), new( 64, 64 ), new( 128, 64 ),
		new( -128, 0 ), new( -64, 0 ), Vector2.Zero, new( 64, 0 ), new( 128, 0 ),
		new( -128, -64 ), new( -64, -64 ), new( 0, -64 ), new( 64, -64 ), new( 128, -64 ),
		new( -64, -128 ), new( 0, -128 ), new( 64, -128 )
	];

	Vector3[] FlamePositions( Vector3 origin )
	{
		var positions = new List<Vector3>();
		foreach ( var offset in FlameOffsets )
		{
			var point = origin + new Vector3( offset.x, offset.y, 0 );
			var hit = Scene.Trace.Ray( origin, point ).IgnoreGameObjectHierarchy( GameObject )
				.WithoutTags( "npc", "player", "playercontroller", "trigger" ).Run();
			if ( !hit.Hit && !hit.StartedSolid && !WaterBody.Find( Scene, point ).IsValid() )
				positions.Add( point );
		}
		return positions.ToArray();
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	async void ShowBlast( Vector3 origin, Vector3 contact, Vector3 normal, float damage, Vector3[] flames )
	{
		if ( Application.IsDedicatedServer || ModelMount is not { } mount ) return;
		var scene = Scene;
		if ( Plasma ) RifleGrenadeSphere.Show( contact, new Color32( 255, 64, 0, 255 ), damage * 2, damage * 2, 0, 0.5f, mount );

		Blast.SpriteEffect( "Rifle grenade explosion", "fexplo", origin, ((damage - 50) * 0.06f).Clamp( 1, 25.5f ), 15, mount: mount );
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
	internal override void OnImpact( IDamageable victim, Vector3 position, Vector3 normal )
	{
		var field = new GameObject( false, "Laser grenade field" ) { WorldPosition = position + normal };
		field.NetworkMode = NetworkMode.Never;
		var effect = field.AddComponent<LaserGrenadeField>();
		effect.Attacker = Shooter;
		effect.Damage = Damage;
		field.Enabled = true;

		ShowField( field.WorldPosition, Damage );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowField( Vector3 origin, float damage )
	{
		if ( Application.IsDedicatedServer || ModelMount is not { } mount ) return;
		RifleGrenadeSphere.Show( origin, new Color32( 255, 96, 96, 255 ), damage * 2, damage * 2.5f,
			LaserGrenadeField.FullDamageTime, LaserGrenadeField.FadeRate, mount );
		NpcAudio.Play( mount.NpcAudio.Find( "doors/aliendoor1.wav" ), origin );
	}
}

[Hide]
public sealed class LaserGrenadeField : Component
{
	internal GameObject Attacker { get; set; }
	internal float Damage { get; set; }

	const float Tick = 0.1f;
	internal const float FullDamageTime = 1;
	internal const float FadeRate = 0.3f;
	TimeSince _age;
	TimeUntil _nextTick = Tick;
	float _radius = 1;

	protected override void OnStart() => _age = 0;

	protected override void OnFixedUpdate()
	{
		if ( _nextTick > 0 ) return;
		_nextTick += Tick;
		var strength = 1 - System.MathF.Max( _age - FullDamageTime, 0 ) * FadeRate;
		if ( strength <= 0 )
		{
			GameObject.Destroy();
			return;
		}

		_radius = System.MathF.Min( _radius + Damage * 0.25f, Damage * 2 );
		Blast.ApplyRadiusDamage( GameObject, Attacker, WorldPosition, Damage * 0.25f * 0.75f * strength,
			_radius, ignoreAttacker: false, "energybeam" );
	}
}

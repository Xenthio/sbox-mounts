using GoldSrc.HalfLife;

namespace GoldSrc.SvenCoop;

[Title( "Robot Grunt" ), Category( "Sven Co-op" )]
[NpcDefinition( "monster_robogrunt", "rgrunt", MonsterClass.Machine, 100, 16, 72, 160,
	Mount = "svencoop", ModelVariants = ["rgruntf"], MetalBody = true, Attack = NpcAttack.Bullet, Damage = 6, Range = 2048,
	ProjectileModel = "grenade", StrikeEvents = [4, 5, 6], Blood = BloodColor.None )]
public sealed class RoboGrunt : HumanGrunt
{
	internal const string Prefab = "mount://svencoop/entities/npcs/monster_robogrunt.prefab";
	[Property, Sync( SyncFlags.FromHost )] public int ClassOverride { get; set; }

	public RoboGrunt()
	{
		Weapon = Loadout.Rifle;
		Skin = 0;
	}

	internal override MonsterClass Classification => ClassOverride switch
	{
		-1 => MonsterClass.None,
		>= 1 and <= 13 => (MonsterClass)ClassOverride,
		_ => base.Classification
	};

	internal override string RadioPrefix => "RB";
	internal override float KickDamage => 12;
	internal override int EffectiveSkin => System.Math.Max( Skin, 0 );
	protected override int Capacity => HasShotgun ? 8 : 30;
	internal override string BurstWeapon => HasLauncher ? "m16a2" : base.BurstWeapon;
	internal override string[] ExtraActions => [.. base.ExtraActions, "standing_m16a2", "crouching_m16a2"];
	internal override NpcSounds Sounds { get; } = new();

	internal override void ApplyMapProperties( Bsp.Entity entity )
	{
		var weapons = entity.GetInt( "weapons" );
		if ( weapons == 0 ) weapons = 3;
		Weapon = (weapons & 8) != 0 ? Loadout.Shotgun : (weapons & 4) != 0 ? Loadout.RifleWithGrenadeLauncher : Loadout.Rifle;
		HandGrenades = (weapons & 2) != 0;
		ClassOverride = entity.GetInt( "classify" );
		if ( ClassOverride == 0 && entity.GetInt( "is_player_ally" ) != 0 )
			ClassOverride = (int)MonsterClass.PlayerAlly;
	}

	internal override string MapModel( Bsp.Entity entity ) => entity.GetString( "new_model" ) is { Length: > 0 } model
		? model : Classification is MonsterClass.PlayerAlly or MonsterClass.HumanPassive ? "models/rgruntf.mdl" : null;

	internal override void ApplyEquipment()
	{
		var group = Weapon switch { Loadout.Shotgun => 1, Loadout.RifleWithGrenadeLauncher => 0, _ => 4 };
		Renderer.SetBodyGroup( "weapons", group );
		if ( Networking.IsHost && Ammo < 0 ) Ammo = Capacity;
	}

	internal override void FireBullet( GameObject target, Vector3 origin, Vector3 end )
	{
		if ( Ammo <= 0 || FriendlyInLine( target ) ) return;
		var shotgun = HasShotgun;
		if ( IsFirstStrike )
		{
			var sample = shotgun ? "weapons/sbarrel1.wav" : HasLauncher
				? "weapons/m16_3round.wav" : $"weapons/hks{Game.Random.Int( 1, 3 )}.wav";
			PlayGunshot( sample, origin );
		}

		Ammo--;
		EjectShell( origin - (end - origin).Normal * 24, WorldRotation, shotgun, Definition.Mount );
		TraceBullets( origin, end, shotgun ? 7 : 1, Definition.Damage, shotgun ? 0.13053f : 0.08716f,
			AttackRange, tracerFrequency: shotgun ? 0 : 4 );
	}

	internal override void ModifyDamage( Component.IDamageable.DamageEvent hit )
	{
		var info = hit.DamageInfo;
		var tags = info.Tags;
		var groups = HitGroups( info );
		var head = groups.Has( "hitgroup_1" ) || groups.Has( "hitgroup_10" );
		var amount = hit.AcceptedDamage;
		if ( tags.Has( "bullet" ) || tags.Has( "club" ) || tags.Has( "sniper" ) )
		{
			if ( ShouldShowArmorImpact() ) ArmorRicochet.Show( info.Position, 1, "svencoop" );
			amount = head ? ReduceArmor( amount, 5, 0.1f ) : amount * 0.15f;
		}
		if ( head )
		{
			if ( tags.Has( "shock" ) ) amount *= 1.5f;
		}
		else
		{
			if ( tags.Has( "slash" ) ) amount = ReduceArmor( amount, 5, 0.08f );
			if ( Health > 10 && (tags.Has( "blast" ) || tags.Has( "explosion" )) ) amount = ReduceArmor( amount, 15, 0.2f );
			if ( Health > 10 && tags.Has( "burn" ) ) amount *= 0.5f;
		}
		hit.AcceptedDamage = amount;
	}

	static float ReduceArmor( float amount, float resistance, float scale ) =>
		(amount >= resistance ? amount - resistance : amount) * scale;

	internal override void DeathSound() => EmitSound( Game.Random.Int( 0, 1 ) == 0 ? "turret/tu_die.wav" : "turret/tu_die2.wav",
		WorldPosition, attenuation: 0.5f );

	internal override NpcCorpse AddCorpseComponent( GameObject go ) => go.AddComponent<RobotWreck>();
}

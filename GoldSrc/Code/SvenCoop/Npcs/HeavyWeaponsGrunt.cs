using GoldSrc.HalfLife;
using Sandbox.Npcs;

namespace GoldSrc.SvenCoop;

[Title( "Heavy Weapons Grunt" ), Category( "Sven Co-op" )]
[NpcDefinition( "monster_hwgrunt", "hwgrunt", MonsterClass.HumanMilitary, 200, 16, 72, 120,
	Mount = "svencoop", Attack = NpcAttack.Bullet, Damage = 15, Range = 2048,
	AttackSequence = "attack", StrikeEvents = [1, 2, 3, 4, 5, 6, 7, 8, 9, 5001], Gibs = GibSet.Human, Blood = BloodColor.Red )]
public sealed partial class HeavyWeaponsGrunt : Npc
{
	public enum Sidearm { Random, Glock, DesertEagle, Python }

	[Property] public bool DisableMinigunDrop { get; set; }
	[Property] public Sidearm BackupWeapon { get; set; } = Sidearm.Glock;
	[Sync( SyncFlags.FromHost ), Change( nameof( OnMinigunChanged ) )]
	public bool HasMinigun { get; private set; } = true;

	GameObject _spinTarget;
	TimeUntil _stunned;
	int _pistolRounds;

	internal override string[] ExtraActions => ["spinup", "spindown", "pistol_idle", "pistol_shoot", "pistol_reload", "pickup_minigun"];
	internal override NpcSounds Sounds { get; } = new( null, null, "hgrunt/gr_pain", "hgrunt/gr_die", null );
	internal override StudioSequence RestSequence => HasMinigun ? base.RestSequence : Find( "pistol_idle" ) ?? base.RestSequence;
	internal override Vector3 AttackOrigin => Renderer.GetAttachment( HasMinigun ? "attachment1" : "attachment2" )?.Position ??
		WorldPosition + Vector3.Up * (EyeHeight * 0.8f);

	int PistolCapacity => BackupWeapon switch { Sidearm.DesertEagle => 7, Sidearm.Python => 6, _ => 17 };

	internal override void ApplyMapProperties( Bsp.Entity entity )
	{
		DisableMinigunDrop = entity.GetInt( "disable_minigun_drop" ) != 0;
		BackupWeapon = (Sidearm)System.Math.Clamp( entity.GetInt( "weapons", 1 ), 0, 3 );
	}

	internal override string MapModel( Bsp.Entity entity ) => entity.GetString( "model" );

	internal override void OnModelLoaded( ModelLoader loader )
	{
		EyeHeight = loader.EyePosition.z;
		if ( BackupWeapon == Sidearm.Random )
			BackupWeapon = (Sidearm)(1 + (uint)GameObject.Id.GetHashCode() % 3);
		_pistolRounds = PistolCapacity;
	}

	internal override void ApplyEquipment()
	{
		if ( Renderer.Model.Parts.All.Any( part => part.Name == "weapons" ) )
			Renderer.SetBodyGroup( "weapons", HasMinigun ? 0 : (int)BackupWeapon );
	}

	void OnMinigunChanged( bool _, bool hasMinigun )
	{
		if ( Renderer.IsValid() && Renderer.Model.IsValid() ) ApplyEquipment();
	}

	internal override void BeginAttack( GameObject target )
	{
		if ( _stunned > 0 || !HasClearShot( target ) ) return;
		if ( !HasMinigun )
		{
			if ( _pistolRounds <= 0 && Find( "pistol_reload" ) is { } reload ) PlayAction( reload );
			else AttackWith( target, Find( "pistol_shoot" ) );
			return;
		}

		_spinTarget = target;
		if ( Find( "spinup" ) is { } spin )
		{
			EmitSound( "hassault/hw_spinup.wav", AttackOrigin );
			PlayAction( spin );
		}
		else AttackWith( target, Find( "attack" ) );
	}

	internal override void FinishAction( string name )
	{
		if ( name is not ("spinup" or "attack") ) return;
		if ( HasMinigun && HasClearShot( _spinTarget ) )
		{
			AttackWith( _spinTarget, Find( "attack" ) );
			return;
		}
		_spinTarget = null;
		if ( Find( "spindown" ) is not { } down ) return;
		EmitSound( "hassault/hw_spindown.wav", AttackOrigin );
		PlayAction( down );
	}

	bool HasClearShot( GameObject target )
	{
		if ( !IsLiveTarget( target ) || !InAttackRange( target, AimAt( target ) ) ) return false;
		var hit = Scene.Trace.Ray( AttackOrigin, AimAt( target ) ).IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "trigger", "playercontroller" ).UseHitboxes().Run();
		return !hit.Hit || hit.GameObject?.IsAncestor( target ) == true;
	}

	internal override void FireBullet( GameObject target, Vector3 origin, Vector3 end )
	{
		if ( !HasClearShot( target ) ) return;
		if ( HasMinigun )
		{
			if ( IsFirstStrike ) PlayGunshot( "hassault/hw_shoot", origin );
			TraceBullets( origin, end, 1, Definition.Damage, 0.13053f, AttackRange );
			return;
		}

		if ( _pistolRounds <= 0 ) return;
		_pistolRounds--;
		var (sound, damage) = BackupWeapon switch
		{
			Sidearm.DesertEagle => ("weapons/desert_eagle_fire.wav", 18f),
			Sidearm.Python => ("weapons/357_shot1.wav", 18f),
			_ => ("weapons/pl_gun3.wav", 9f)
		};
		PlayGunshot( sound, origin );
		TraceBullets( origin, end, 1, damage, 0.08716f, AttackRange, tracerFrequency: 0 );
	}

	internal override void HandleActionEvent( int id )
	{
		if ( Action == "pistol_reload" && id == 2 ) _pistolRounds = PistolCapacity;
		if ( Action == "pickup_minigun" && id == 12 ) RecoverMinigun();
	}

	protected override void OnDamaged( Component.IDamageable.DamageEvent hit )
	{
		base.OnDamaged( hit );
		if ( Health <= 0 || !HasMinigun || DisableMinigunDrop || hit.AcceptedDamage <= 50 ||
			!hit.DamageInfo.Tags.Has( "blast" ) || Find( "pickup_minigun" ) is null || Game.Random.Int( 1, 10 ) == 10 ) return;
		if ( !DropMinigun() ) return;
		InterruptAction();
		_stunned = 3;
		var danger = hit.Instigator.IsValid() ? hit.Instigator.WorldPosition : hit.DamageInfo.Origin;
		var away = (WorldPosition - danger).WithZ( 0 ).Normal;
		MoveToward( FindCover( danger, hit.Instigator, true ) ?? WorldPosition + away * 180, 16 );
	}
}

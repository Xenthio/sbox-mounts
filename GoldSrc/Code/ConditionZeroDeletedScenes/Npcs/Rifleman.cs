using GoldSrc.HalfLife;

namespace GoldSrc.ConditionZeroDeletedScenes;

[Hide]
public abstract class Rifleman : CampaignSoldier
{
	[Sync( SyncFlags.FromHost )] int Ammo { get; set; } = -1;

	Firearm? _firearm;
	Firearm Weapon => _firearm ??= SelectFirearm( ClassName, IsCounterTerrorist );

	internal override float AttackRange => WeaponRange > 0 ? WeaponRange : Weapon.Range;
	internal override float StrikeRange => AttackRange;
	internal override int WeaponBodyGroup => Weapon.BodyGroup;
	internal override Vector3 AttackOrigin => WorldPosition + Vector3.Up * 60;
	internal override string[] ExtraActions => [Weapon.FireSequence, Weapon.ReloadSequence];

	internal override void ApplyEquipment()
	{
		base.ApplyEquipment();
		if ( Networking.IsHost && Ammo < 0 ) Ammo = Weapon.Capacity;
	}

	internal override void BeginAttack( GameObject target )
	{
		if ( Ammo == 0 )
		{
			if ( Find( Weapon.ReloadSequence ) is { } reload ) PlayAction( reload );
			return;
		}

		if ( Find( Weapon.FireSequence ) is { } attack ) AttackWith( target, attack );
	}

	internal override void HandleActionEvent( int id )
	{
		if ( id != 2 || Action != Weapon.ReloadSequence ) return;
		Ammo = Weapon.Capacity;
		EmitSound( $"weapons/{Weapon.ReloadSample}.wav", WorldPosition );
	}

	internal override SavedMagazine CaptureMagazine() => new( Ammo, 0 );
	internal override void RestoreMagazine( SavedMagazine magazine ) => Ammo = magazine.Rounds;

	internal override void FireBullet( GameObject target, Vector3 origin, Vector3 end )
	{
		if ( Ammo <= 0 ) return;
		var weapon = Weapon;
		var burst = weapon.Glock && Ammo >= 3 && Game.Random.Int( 0, 99 ) < 15;
		var rounds = burst ? 3 : 1;
		Ammo -= rounds;
		var muzzle = Renderer.GetAttachment( "attachment1" )?.Position ?? origin;
		var sample = weapon.SecondReport is { } variant && Game.Random.Int( 0, 1 ) == 1 ? variant : weapon.Report;
		if ( burst ) sample = "glock18-1";
		PlayGunshot( $"weapons/{sample}.wav", muzzle );
		EjectShell( origin + (end - origin).Normal * 8, WorldRotation, shotgun: weapon.Pellets > 1 );

		// The BSP stores a full cone angle in degrees.
		var spread = SpreadFor( weapon.Spread );
		TraceBullets( origin, end, weapon.Pellets * rounds, weapon.Damage, spread, weapon.Range, weapon.Tracers );
	}

	readonly record struct Firearm( int BodyGroup, int Capacity, string FireSequence, string ReloadSequence, string ReloadSample,
		string Report, string SecondReport, int Pellets, float Damage, float Range, float Spread, int Tracers, bool Glock = false );

	static Firearm SelectFirearm( string className, bool counterTerrorist )
	{
		var suffix = className?[(className.LastIndexOf( '_' ) + 1)..];
		return suffix switch
		{
			"pistol" => new( 5, 16, "pistol_standing", "pistol_reload", "pistol_reload",
				counterTerrorist ? "usp_unsil-1" : "glock18-2", null, 1, 5, 1024, 0.08716f, 0, Glock: !counterTerrorist ),
			"shotgun" => new( 1, 8, "standing_shotgun", "reload_shotgun", "reload_shotgun",
				counterTerrorist ? "xm1014-1" : "sbarrel1", null, counterTerrorist ? 8 : 5, 10, 2048, 0.13053f, 0 ),
			"mp5" => new( 0, 36, "standing_mp5", "reload_mp5", "reload_mp5",
				"mp5-1", "mp5-2", 1, 10, 2048, 0.08716f, counterTerrorist ? 4 : 6 ),
			"smg" => new( counterTerrorist ? 7 : 8, 45, "shootcycle_tmp", "reload_mp5", "reload_mp5",
				counterTerrorist ? "mac10-1" : "tmp-1", counterTerrorist ? null : "tmp-2", 1, 4, 2048, 0.08716f, 4 ),
			"machinegun" => new( counterTerrorist ? 6 : 7, 36, "standing_mp5", "reload_mp5", "reload_mp5",
				"m60-1", "m60-2", 1, 15, 2048, 0.08716f, 8 ),
			"sniperrifle" => new( 3, counterTerrorist ? 36 : 3, "sniper-fire", "reload_mp5", "reload_mp5",
				counterTerrorist ? "scout_fire-1" : "awp1", null, 1, 45, 4096, 0.04362f, 0 ),
			_ => new( 4, 30, "standing_mp5", "reload_mp5", "reload_mp5",
				counterTerrorist ? "m4a1_unsil-1" : "ak47-1", counterTerrorist ? "m4a1_unsil-2" : "ak47-2", 1, 12, 2048, 0.08716f, 4 )
		};
	}
}

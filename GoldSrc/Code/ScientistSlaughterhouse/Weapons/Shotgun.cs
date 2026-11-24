using GoldSrc.HalfLife;
using GoldSrc.Maps;

namespace GoldSrc.ScientistSlaughterhouse;

[Title( "Shotgun" ), Category( "Scientist Slaughterhouse" )]
[WeaponPickup( "shotgun", "buckshot", 125, Bucket = 2, Weight = 15, Mount = "ssh" )]
public sealed class Shotgun : PumpShotgun
{
	const float BuckshotDamage = 5;
	const string LaserSprite = "mount://ssh/valve/sprites/glow01.spr";
	static readonly TagSet BlastTags = ["blast"];

	enum ShellType { Conventional, Explosive, Laser }

	[Sync( SyncFlags.FromHost )] ShellType Shell { get; set; }
	TimeUntil _modeReady, _specialReady;

	internal override (float Damage, int Pellets) ShellDamage => (BuckshotDamage, Deathmatch ? 4 : 6);
	internal override Vector2 PelletSpread => Deathmatch
		? new Vector2( DoubleShot ? 0.17365f : 0.08716f, 0.04362f )
		: base.PelletSpread;
	public override Ray AimRay => Owner.IsValid() ? Owner.EyeTransform.ForwardRay : base.AimRay;

	protected override bool WantsReload() => !Input.Down( "use" ) && base.WantsReload();

	protected override void Think()
	{
		base.Think();
		if ( Input.Down( "use" ) && Input.Pressed( "reload" ) ) CycleShells();
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void CycleShells()
	{
		if ( !IsActive || !Inventory.IsValid() || IsReloading || _modeReady > 0 || _specialReady > 0 || NextPrimaryFire > 0 ||
			(Clip1 <= 0 && Ammo1 <= 0) ) return;

		_modeReady = 1;
		Inventory.GiveAmmo( PrimaryAmmoType, Clip1 );
		Clip1 = 0;
		Shell = Shell == ShellType.Laser ? ShellType.Conventional : Shell + 1;
		ShowShellType( Shell );
		Reload();
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowShellType( ShellType shell )
	{
		if ( IsProxy || Application.IsDedicatedServer ) return;
		Scene.GetSystem<MessageDisplay>()?.Show( new TitleMessage
		{
			Text = $"{shell} rounds loaded",
			Position = new Vector2( -1, 0.5f ),
			Color = Color.White,
			HoldTime = 1,
			FadeOut = 0.25f
		} );
	}

	internal override bool TryFireSpecialShells( bool both )
	{
		if ( Shell == ShellType.Conventional ) return false;
		FireSpecialShells( both );
		return true;
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void FireSpecialShells( bool both )
	{
		if ( !IsActive || !Owner.IsValid() || IsReloading || _specialReady > 0 || Shell == ShellType.Conventional ||
			WaterBody.Find( Scene, Owner.EyePosition ).IsValid() || !TakePrimaryAmmo( both ? 2 : 1 ) ) return;

		_specialReady = both ? SecondaryDelay : PrimaryDelay;

		var eye = Owner.EyeTransform.ForwardRay;
		var target = ProjectileTarget.Find( Scene, Owner.GameObject, eye, range: 2048, minimumAlignment: 0.9848f );
		var aim = target.IsValid() ? (GoldSrc.HalfLife.Npc.TargetBodyPosition( target ) - eye.Position).Normal : eye.Forward;
		var basis = Rotation.LookAt( aim );
		var laser = Shell == ShellType.Laser;
		var count = laser ? (both ? 12 : 6) : (both ? 6 : 1);
		var spread = laser ? 0.08716f : (both ? 0.13053f : 0.00873f);
		var impacts = laser ? null : new List<Vector3>( count );
		for ( int i = 0; i < count; ++i )
		{
			var offset = SamplePelletOffset() * spread;
			var direction = aim + basis.Right * offset.x + basis.Up * offset.y;
			if ( laser )
				NpcProjectile.Fire<ShotgunLaserPulse>( Owner.GameObject, LaserSprite, BuckshotDamage * 1.4f, null, eye.Position,
					direction.Normal, Game.Random.Float( 1300, 1400 ) );
			else
				ExplodePellet( eye.Position, direction, BuckshotDamage * (both ? 2 : 6), impacts );
		}
		ShowSpecialShot( both, impacts?.ToArray() ?? [] );
	}

	void ExplodePellet( Vector3 start, Vector3 direction, float damage, List<Vector3> impacts )
	{
		var hit = TraceLineOfFire( new Ray( start, direction.Normal ), Ballistics.Range * direction.Length, 0 )
			.WithoutTags( "trigger" ).UseHitboxes().Run();
		ShootBullet( hit, damage, 0, BlastTags );

		// Explosive shells trigger env_explosion's downward probe at each trace endpoint.
		var floor = Scene.Trace.Ray( hit.EndPosition + Vector3.Up * 8, hit.EndPosition + Vector3.Down * 32 )
			.IgnoreGameObjectHierarchy( Owner.GameObject ).WithoutTags( "npc", "player", "trigger", "playercontroller" ).Run();
		var origin = floor.Hit && !floor.StartedSolid
			? floor.EndPosition + floor.Normal * ((damage - 24) * 0.6f)
			: hit.Hit ? hit.EndPosition + hit.Normal * 0.5f : hit.EndPosition;
		Blast.DamageInRadius( GameObject, Owner.GameObject, origin, damage, damage * 2.5f, "blast" );
		impacts.Add( origin );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowSpecialShot( bool both, Vector3[] impacts )
	{
		var delay = both ? SecondaryDelay : PrimaryDelay;
		SetNextFire( delay );
		PlayShotReport( both );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( both ? "shoot_big" : "shoot", idleAfter: Clip1 == 0 ? delay : both ? 6 : 5 );
		PostWeaponEvent( WeaponEvents.Attack );
		ShowPlayerMuzzleFlash();
		if ( Application.IsDedicatedServer || ContentMount is not { } mount ) return;
		foreach ( var point in impacts )
		{
			Blast.SpriteEffect( "Explosive buckshot flash", "zerogxplode", point, 1, 15, mount: mount );
			NpcAudio.Play( mount.NpcAudio.Find( $"weapons/explode{Game.Random.Int( 3, 5 )}.wav" ), point, attenuation: 0.3f );
		}
	}
}

[Hide]
public sealed class ShotgunLaserPulse : LaserPulse
{
	protected override Color Tint => new( 1, 16f / 255, 16f / 255 );
	protected override float GlowScale => 0.5f;
}

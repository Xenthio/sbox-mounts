using GoldSrc.HalfLife;
using GoldSrc.Maps;

namespace GoldSrc.ScientistSlaughterhouse;

[Title( "Crossbow" ), Category( "Scientist Slaughterhouse" )]
[WeaponPickup( "crossbow", "bolts", 100, Bucket = 2, Weight = 10, Mount = "ssh" )]
public sealed class Crossbow : ClassicCrossbow
{
	enum DartType { Standard, Explosive, Flare, Electrical, LaserNet }

	[Sync( SyncFlags.FromHost )] DartType Dart { get; set; }
	[Sync( SyncFlags.FromHost )] bool BurstActive { get; set; }
	TimeUntil _nextDartChange;
	const float BurstInterval = 0.1f;

	internal override int FreshPickupRounds => 10;
	internal override bool UseScopedHitscan => false;

	public override Ray AimRay => Owner.IsValid() ? OffsetLaunchRay( new Vector3( 0, 0, -2 ) ) : base.AimRay;

	protected override bool WantsReload() => !Input.Down( "use" ) && base.WantsReload();

	protected override void Think()
	{
		base.Think();
		if ( Input.Down( "use" ) && Input.Pressed( "reload" ) ) CycleDarts();
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void CycleDarts()
	{
		if ( !IsActive || !Inventory.IsValid() || IsReloading || _nextDartChange > 0 ||
			BurstActive || NextPrimaryFire > 0 || (Clip1 <= 0 && Ammo1 <= 0) ) return;

		_nextDartChange = 1;
		Inventory.GiveAmmo( PrimaryAmmoType, Clip1 );
		Clip1 = 0;
		Dart = Dart == DartType.LaserNet ? DartType.Standard : Dart + 1;
		ShowDartType( Dart );
		Reload();
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowDartType( DartType dart )
	{
		if ( IsProxy || Application.IsDedicatedServer ) return;
		Scene.GetSystem<MessageDisplay>()?.Show( new TitleMessage
		{
			Text = dart == DartType.LaserNet ? "Laser net darts loaded" : $"{dart} darts loaded",
			Position = new Vector2( -1, 0.5f ),
			Color = Color.White,
			HoldTime = 1,
			FadeOut = 0.25f
		} );
	}

	public override bool CanPrimaryAttack() => !BurstActive && base.CanPrimaryAttack();
	public override bool CanSecondaryAttack() => !BurstActive && base.CanSecondaryAttack();
	public override bool CanReload() => !BurstActive && base.CanReload();

	public override void PrimaryAttack()
	{
		if ( Input.Down( "attack2" ) ) StartBurst( AimRay );
		else base.PrimaryAttack();
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void StartBurst( Ray ray )
	{
		if ( BurstActive || IsReloading ) return;
		BurstActive = Clip1 > 1;
		if ( !FireBurstDart( ray ) ) BurstActive = false;
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();
		if ( !Networking.IsHost || !BurstActive || !IsPrimaryLaunchReady ) return;
		if ( !IsActive || !Owner.IsValid() || IsReloading || !FireBurstDart( AimRay ) )
			BurstActive = false;
	}

	protected override void OnHolstered()
	{
		if ( Networking.IsHost ) BurstActive = false;
		base.OnHolstered();
	}

	bool FireBurstDart( Ray ray )
	{
		var jitter = SampleBurstOffset();
		var aim = Rotation.LookAt( ray.Forward );
		var spread = aim.Right * jitter.x + aim.Up * jitter.y;
		var last = Clip1 <= 1;
		if ( !TryLaunch( new Ray( ray.Position, (ray.Forward + spread).Normal ), Release,
			extraDelay: last ? 0 : BurstInterval - PrimaryDelay ) ) return false;

		if ( last )
		{
			BurstActive = false;
			EndBurst();
		}
		return true;
	}

	static Vector2 SampleBurstOffset()
	{
		Vector2 offset;
		do
		{
			offset = new Vector2( Game.Random.Float( -0.5f, 0.5f ) + Game.Random.Float( -0.5f, 0.5f ),
				Game.Random.Float( -0.5f, 0.5f ) + Game.Random.Float( -0.5f, 0.5f ) );
		}
		while ( offset.LengthSquared > 1 );
		return offset * 0.3473f;
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void EndBurst() => SetNextFire( PrimaryDelay );

	internal override CrossbowBolt SpawnBolt( Ray ray, float speed )
	{
		if ( ContentMount is not { } mount || mount.FindMountPath( "models/crossbow_bolt.mdl" ) is not { } path ) return null;
		var model = $"mount://{mount.Ident}/{path}.vmdl";
		return Dart switch
		{
			DartType.Explosive => NpcProjectile.Fire<ExplosiveDart>( Owner.GameObject, model, 40, null, ray.Position, ray.Forward, speed ),
			DartType.Flare => NpcProjectile.Fire<FlareDart>( Owner.GameObject, model, 25, null, ray.Position, ray.Forward, speed ),
			DartType.Electrical => NpcProjectile.Fire<ElectricalDart>( Owner.GameObject, model, 25, null, ray.Position, ray.Forward, speed ),
			DartType.LaserNet => NpcProjectile.Fire<LaserNetDart>( Owner.GameObject, model, 25, null, ray.Position, ray.Forward, speed ),
			_ => NpcProjectile.Fire<CrossbowDart>( Owner.GameObject, model, 50, null, ray.Position, ray.Forward, speed )
		};
	}
}

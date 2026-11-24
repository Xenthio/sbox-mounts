using GoldSrc.HalfLife;
using GoldSrc.Maps;

namespace GoldSrc.ScientistSlaughterhouse;

[Title( "Crossbow" ), Category( "Scientist Slaughterhouse" )]
[WeaponPickup( "crossbow", "bolts", 100, Bucket = 2, Weight = 10, Mount = "ssh" )]
public sealed class Crossbow : ClassicCrossbow
{
	enum DartType { Standard, Explosive, Flare }

	[Sync( SyncFlags.FromHost )] DartType Dart { get; set; }
	TimeUntil _nextDartChange;

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
			NextPrimaryFire > 0 || (Clip1 <= 0 && Ammo1 <= 0) ) return;

		_nextDartChange = 1;
		Inventory.GiveAmmo( PrimaryAmmoType, Clip1 );
		Clip1 = 0;
		Dart = Dart == DartType.Flare ? DartType.Standard : Dart + 1;
		ShowDartType( Dart );
		Reload();
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowDartType( DartType dart )
	{
		if ( IsProxy || Application.IsDedicatedServer ) return;
		Scene.GetSystem<MessageDisplay>()?.Show( new TitleMessage
		{
			Text = $"{dart} darts loaded",
			Position = new Vector2( -1, 0.5f ),
			Color = Color.White,
			HoldTime = 1,
			FadeOut = 0.25f
		} );
	}

	internal override CrossbowBolt SpawnBolt( Ray ray, float speed )
	{
		if ( ContentMount is not { } mount || mount.FindMountPath( "models/crossbow_bolt.mdl" ) is not { } path ) return null;
		var model = $"mount://{mount.Ident}/{path}.vmdl";
		return Dart switch
		{
			DartType.Explosive => NpcProjectile.Fire<ExplosiveDart>( Owner.GameObject, model, 40, null, ray.Position, ray.Forward, speed ),
			DartType.Flare => NpcProjectile.Fire<FlareDart>( Owner.GameObject, model, 25, null, ray.Position, ray.Forward, speed ),
			_ => NpcProjectile.Fire<CrossbowDart>( Owner.GameObject, model, 50, null, ray.Position, ray.Forward, speed )
		};
	}
}

using GoldSrc.HalfLife;
using GoldSrc.Maps;

namespace GoldSrc.ScientistSlaughterhouse;

[Title( "Satchel Charge" ), Category( "Scientist Slaughterhouse" )]
[WeaponPickup( "satchel", "satchels", 50, AlternateModel = "satchel_radio", Bucket = 4, Weight = -10, Mount = "ssh" )]
public sealed class Satchel : ClassicSatchel
{
	[Sync( SyncFlags.FromHost )] bool SatelliteRain { get; set; }

	internal override bool DetonateWithPrimary => true;
	internal override GameMount ChargeMount => ContentMount;
	internal override float RadioRange => float.PositiveInfinity;
	internal override bool UsesRadioForThrow => !SatelliteRain;

	public override Ray AimRay => Owner.IsValid()
		? new Ray( Owner.WorldPosition + Vector3.Up * (Owner.CurrentHeight * 0.5f), Owner.EyeTransform.Forward )
		: base.AimRay;

	internal override void Configure()
	{
		base.Configure();
		StartingAmmo = 5;
	}

	internal override bool SpawnCharge( Ray ray ) => SatelliteRain
		? PlacedCharge.SpawnSatchel<SatelliteCharge>( Owner, ray, ChargeMount, damage: 150 )
		: base.SpawnCharge( ray );

	protected override void Think()
	{
		base.Think();
		if ( Input.Down( "use" ) && Input.Pressed( "reload" ) ) SelectPayload();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( Networking.IsHost && IsActive && IsHeld && !IsRadioOut && IsPrimaryLaunchReady && !HasPrimaryAmmo() )
			Inventory.Remove( this );
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void SelectPayload()
	{
		if ( !IsActive || !Owner.IsValid() ) return;
		SatelliteRain = !SatelliteRain;
		ShowPayload( SatelliteRain );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowPayload( bool satellite )
	{
		if ( IsProxy || Application.IsDedicatedServer ) return;
		Scene.GetSystem<MessageDisplay>()?.Show( new TitleMessage
		{
			Text = satellite ? "Satellite Rain selected" : "Satchel Charge selected",
			Position = new Vector2( -1, 0.5f ),
			Color = Color.White,
			HoldTime = 1,
			FadeOut = 0.25f
		} );
	}
}

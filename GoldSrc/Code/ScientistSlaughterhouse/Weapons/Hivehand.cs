using GoldSrc.HalfLife;
using GoldSrc.Maps;

namespace GoldSrc.ScientistSlaughterhouse;

internal enum HornetColor { Red, Green, Blue, Yellow, Orange, LightBlue }

[Title( "Hivehand" ), Category( "Scientist Slaughterhouse" )]
[WeaponPickup( "hgun", "hornets", 24, ClassName = "hornetgun", Bucket = 6, Weight = 10, Mount = "ssh" )]
public sealed class Hivehand : ClassicHivehand
{
	[Sync( SyncFlags.FromHost )] internal HornetColor SelectedHornet { get; private set; }
	TimeUntil _nextSelection;

	internal override float RechargeInterval => 0.5f;
	internal override float EmptyPrimaryDelay => 0;

	internal override void Configure()
	{
		base.Configure();
		StartingAmmo = 24;
		SecondaryDelay = 0.5f;
	}

	internal override bool ReleaseTrackingHornet( Ray ray ) =>
		SpawnHornet<ColoredHornet>( ray.Position, ray.Forward, 300, hornet => hornet.Variant = SelectedHornet );

	public override bool HasSecondaryAmmo() => true;
	public override void SecondaryAttack() => CycleHornet();

	[Rpc.Host( NetFlags.OwnerOnly )]
	void CycleHornet()
	{
		if ( !IsActive || !Owner.IsValid() || _nextSelection > 0 ) return;
		_nextSelection = SecondaryDelay;
		SelectedHornet = SelectedHornet == HornetColor.LightBlue ? HornetColor.Red : SelectedHornet + 1;
		ShowSelection( SelectedHornet );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowSelection( HornetColor variant )
	{
		if ( IsProxy || Application.IsDedicatedServer ) return;
		var name = variant == HornetColor.LightBlue ? "Lightblue" : variant.ToString();
		Scene.GetSystem<MessageDisplay>()?.Show( new TitleMessage
		{
			Text = $"{name} hornet selected",
			Position = new Vector2( -1, 0.5f ),
			Color = Color.White,
			HoldTime = 1,
			FadeOut = 0.25f
		} );
	}
}

using GoldSrc.HalfLife;

namespace GoldSrc.HalfLifeMMod;

[Title( "Satchel Charge" ), Category( "Half-Life: MMod" )]
[WeaponPickup( "satchel", "satchels", 5, AlternateModel = "satchel_radio", Bucket = 4, Weight = -10, Mount = "hl1mmod" )]
public sealed class Satchel : ClassicSatchel
{
	bool _hasDeployed;

	internal override bool DetonateWithPrimary => true;
	internal override GameMount ChargeMount => ContentMount;
	internal override string DeploySequence => _hasDeployed || IsRadioOut ? "draw" : "draw_first";

	public override Ray AimRay => Owner.IsValid()
		? new Ray( Owner.WorldPosition + Vector3.Up * (Owner.CurrentHeight * 0.5f), Owner.EyeTransform.Forward )
		: base.AimRay;

	protected override void OnEquipped()
	{
		DeployTime = 0.5f;
		base.OnEquipped();
		_hasDeployed = true;
	}

	protected override bool OnAdding( BaseInventoryComponent inventory )
	{
		if ( Networking.MaxPlayers > 1 && inventory.Items.OfType<Satchel>().Any( satchel => satchel != this && satchel.IsRadioOut ) )
			return false;
		return base.OnAdding( inventory );
	}

	internal override bool SpawnCharge( Ray ray ) =>
		PlacedCharge.SpawnSatchel<SatchelCharge>( Owner, ray, ChargeMount, damage: 150 );
}

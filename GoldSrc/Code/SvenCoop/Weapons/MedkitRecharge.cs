using GoldSrc.Maps;

namespace GoldSrc.SvenCoop;

// Holstered inventory items are disabled, so the recharge clock belongs to their holder.
[Hide]
public sealed class MedkitRecharge : Component
{
	Medkit _medkit;
	float _progress;

	internal void Bind( Medkit medkit )
	{
		_medkit = medkit;
		_progress = 0;
	}

	internal void SupplyCharge( int amount )
	{
		if ( _medkit.IsValid() && _medkit.Inventory?.GameObject == GameObject && _medkit.PrimaryAmmoType is { } charge )
			_medkit.Inventory.GiveAmmo( charge, amount );
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;
		if ( !_medkit.IsValid() || _medkit.Inventory?.GameObject != GameObject )
		{
			Destroy();
			return;
		}

		if ( !_medkit.Owner.IsValid() || MapState.PlayerVitals( _medkit.Owner ) is not { Health: > 0 } ) return;
		_progress += Time.Delta * (_medkit.IsActive ? 0.5f : 0.2f);
		if ( _progress < 1.5f ) return;
		_progress -= 1.5f;
		SupplyCharge( 5 );
	}
}

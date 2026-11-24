namespace GoldSrc.Maps;

[Hide]
public sealed class PlayerEquip : MapEntity
{
	[Property] public bool UseOnly { get; set; }
	[Property] public Dictionary<string, int> Weapons { get; set; } = [];
	[Property] public Dictionary<string, int> Ammo { get; set; } = [];
	[Property] public Dictionary<string, int> SuitItems { get; set; } = [];

	readonly HashSet<PlayerController> _spawnChecked = [];
	TimeUntil _nextCheck;

	internal bool HasCheckedSpawn( PlayerController player ) => _spawnChecked.Contains( player );
	internal void MarkSpawnChecked( PlayerController player )
	{
		if ( player.IsValid() ) _spawnChecked.Add( player );
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost || UseOnly || _nextCheck > 0 || !CanTrigger ) return;
		if ( Map.GetComponentInParent<MapInstance>()?.GetComponent<CheckpointLoader>() is { Ready: false } ) return;
		_nextCheck = 0.25f;
		_spawnChecked.RemoveWhere( player => !player.IsValid() );
		foreach ( var player in Scene.GetAllComponents<PlayerController>() )
		{
			if ( HasCheckedSpawn( player ) || GetLivingInventory( player ) is not { } inventory ) continue;
			MarkSpawnChecked( player );
			if ( IsMasterTriggered( player.GameObject ) ) Equip( player, inventory );
		}
	}

	public override void Trigger( GameObject activator = null )
	{
		if ( Networking.IsHost && CanTrigger && activator.IsValid() && activator.GetComponentInParent<PlayerController>() is { } player &&
			GetLivingInventory( player ) is { } inventory ) Equip( player, inventory );
	}

	public override void TurnOff( GameObject activator = null ) => Trigger( activator );

	static BaseInventoryComponent GetLivingInventory( PlayerController player ) => MapState.PlayerVitals( player ) is { Health: > 0 }
		? player.GetComponentInChildren<BaseInventoryComponent>()
		: null;

	void Equip( PlayerController player, BaseInventoryComponent inventory )
	{
		// The long-jump module and batteries require the suit, which can be granted in the same loadout.
		if ( SuitItems.ContainsKey( "item_suit" ) ) SuitItem.Grant( player, "item_suit" );
		foreach ( var (item, count) in SuitItems )
		{
			if ( item == "item_suit" ) continue;
			for ( int i = 0; i < count; i++ )
				if ( !SuitItem.Grant( player, item ) ) break;
		}
		foreach ( var (path, count) in Weapons )
			for ( int i = 0; i < count; i++ )
				inventory.Pickup( path );
		foreach ( var (path, count) in Ammo )
			if ( GameResource.Load<BaseAmmoResource>( path ) is { } ammo )
				inventory.GiveAmmo( ammo, count );
		if ( !inventory.ActiveItem.IsValid() ) inventory.SwitchToBest();
	}
}

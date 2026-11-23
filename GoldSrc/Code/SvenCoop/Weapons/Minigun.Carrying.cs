namespace GoldSrc.SvenCoop;

partial class Minigun
{
	const float PickupReach = 96;
	bool _pressedForPickup;
	PlayerController _limitedPlayer;
	(float Walk, float Run, float Crouch, float Jump) _originalMovement;
	float _speedScale = 1;

	protected override bool OnCanPickup( BaseInventoryComponent inventory ) => _pressedForPickup && base.OnCanPickup( inventory );

	bool Component.IPressable.CanPress( IPressable.Event e ) =>
		!Inventory.IsValid() && e.Source?.GetComponentInParent<BaseInventoryComponent>() is { } inventory
		&& !inventory.Items.OfType<Minigun>().Any();

	bool Component.IPressable.Press( IPressable.Event e )
	{
		if ( !((IPressable)this).CanPress( e ) || e.Source.GetComponentInParent<PlayerController>() is not { } player ) return false;
		PickUp( player.GameObject );
		return true;
	}

	Component.IPressable.Tooltip? Component.IPressable.GetTooltip( IPressable.Event e ) =>
		((IPressable)this).CanPress( e ) ? new IPressable.Tooltip( DisplayName, "touch_app", "Pick up" ) : null;

	[Rpc.Host]
	void PickUp( GameObject source )
	{
		if ( Inventory.IsValid() || source?.GetComponent<PlayerController>() is not { } player ||
			source.GetComponent<BaseInventoryComponent>() is not { } inventory || inventory.Items.OfType<Minigun>().Any() ||
			(Rpc.Caller != Connection.Local && player.Network.Owner != Rpc.Caller) ||
			player.EyePosition.Distance( WorldPosition ) > PickupReach ) return;

		var reach = Scene.Trace.Ray( player.EyePosition, WorldPosition ).IgnoreGameObjectHierarchy( source )
			.IgnoreGameObjectHierarchy( GameObject ).WithoutTags( "trigger" ).Run();
		if ( reach.Hit ) return;

		_pressedForPickup = true;
		try { inventory.PickupWorldItem( this ); }
		finally { _pressedForPickup = false; }
		if ( Inventory == inventory && inventory.ActiveItem != this ) inventory.Switch( this );
	}

	protected override bool OnDrop()
	{
		var inventory = Inventory;
		var rounds = Ammo1;
		if ( !base.OnDrop() ) return false;
		StartingAmmo = rounds == int.MaxValue ? 0 : inventory?.TakeAmmo( PrimaryAmmoType, rounds ) ?? 0;
		return true;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( _spinSound.IsValid() ) _spinSound.Position = WorldPosition;
		if ( _fireSound.IsValid() ) _fireSound.Position = WorldPosition;
		if ( IsProxy || !IsActive || Owner is not { } player )
		{
			RestoreMovement();
			return;
		}

		var scale = Barrels switch
		{
			BarrelState.Firing => 0.01f,
			BarrelState.Idle or BarrelState.Stopping => 0.67f,
			_ => 0.33f
		};
		if ( _limitedPlayer == player && scale == _speedScale ) return;
		RestoreMovement();
		_limitedPlayer = player;
		_originalMovement = (player.WalkSpeed, player.RunSpeed, player.DuckedSpeed, player.JumpSpeed);
		_speedScale = scale;
		player.WalkSpeed *= scale;
		player.RunSpeed *= scale;
		player.DuckedSpeed *= scale;
		player.JumpSpeed = 0;
	}

	void RestoreMovement()
	{
		if ( _limitedPlayer.IsValid() )
		{
			if ( _limitedPlayer.WalkSpeed == _originalMovement.Walk * _speedScale ) _limitedPlayer.WalkSpeed = _originalMovement.Walk;
			if ( _limitedPlayer.RunSpeed == _originalMovement.Run * _speedScale ) _limitedPlayer.RunSpeed = _originalMovement.Run;
			if ( _limitedPlayer.DuckedSpeed == _originalMovement.Crouch * _speedScale ) _limitedPlayer.DuckedSpeed = _originalMovement.Crouch;
			if ( _limitedPlayer.JumpSpeed == 0 ) _limitedPlayer.JumpSpeed = _originalMovement.Jump;
		}
		_limitedPlayer = null;
	}
}

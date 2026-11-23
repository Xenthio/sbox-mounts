namespace GoldSrc.DayOfDefeat;

[Hide]
public abstract class LiveGrenadeWeapon : DayOfDefeatGrenadeWeapon
{
	TimeUntil? _fuse;

	internal override bool HasGrenadesLeft => false;
	internal override string DeploySequence => "exploding_draw";
	internal override string PinSequence => "exploding_pinpull";
	internal override string PinSound => null;
	internal override (string Name, float? Delay) IdleAnimation => ("exploding_idle", 0.5f);

	internal override void Configure()
	{
		base.Configure();
		UsesAmmo = false;
		StartingAmmo = 0;
	}

	protected override bool OnAdding( BaseInventoryComponent inventory ) =>
		!inventory.Items.OfType<LiveGrenadeWeapon>().Any( item => item != this ) && base.OnAdding( inventory );

	protected override void OnAdded( BaseInventoryComponent inventory )
	{
		base.OnAdded( inventory );
		_fuse ??= (TimeUntil)FreshFuse;
	}

	internal void SetFuse( float? seconds ) => _fuse = seconds is { } time ? (TimeUntil)time : null;

	protected override bool OnHolstering( BaseInventoryItem next ) => _fuse is null && base.OnHolstering( next );
	protected override bool OnDrop() => false;

	protected override void OnRemoved( BaseInventoryComponent inventory )
	{
		if ( _fuse is { } fuse ) DayOfDefeatGrenade.Throw( inventory.GameObject, inventory.WorldPosition + Vector3.Up * 20,
			Vector3.Zero, fuse, Definition.Model, underhand: true );
		_fuse = null;
		base.OnRemoved( inventory );
	}

	protected override void OnUpdate()
	{
		if ( Networking.IsHost && _fuse is { } fuse && fuse <= 0 && Owner is { } owner )
		{
			_fuse = null;
			DayOfDefeatGrenade.Explode( GameObject, owner.GameObject, owner.EyePosition );
			Inventory.Remove( this );
			return;
		}
		base.OnUpdate();
	}

	internal override string LaunchGrenade( Vector3 origin, Vector3 direction, float pitch, float pinAge )
	{
		if ( _fuse is { } fuse ) DayOfDefeatGrenade.Throw( Owner.GameObject, origin, LaunchVelocity( direction, pitch ),
			fuse, Definition.Model, Underhand );
		_fuse = null;
		return "exploding_throw";
	}
}

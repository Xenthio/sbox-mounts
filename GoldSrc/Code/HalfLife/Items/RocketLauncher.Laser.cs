namespace GoldSrc.HalfLife;

public abstract partial class GuidedRocketLauncher
{
	[Sync( SyncFlags.FromHost )] protected bool LaserEnabled { get; private set; } = true;
	[Sync( SyncFlags.FromHost )] internal int ActiveRockets { get; set; }
	TimeUntil _nextToggle, _spotSuspended;
	SpriteRenderer _dot;

	internal Vector3? SpotPosition { get; private set; }
	bool GuidingRocket => LaserEnabled && ActiveRockets > 0;

	public override bool CanReload() => !GuidingRocket && base.CanReload();
	protected override bool OnHolstering( BaseInventoryItem next ) => !GuidingRocket && base.OnHolstering( next );

	public override void SecondaryAttack() => ToggleLaser();

	[Rpc.Host( NetFlags.OwnerOnly )]
	void ToggleLaser()
	{
		if ( !IsActive || IsReloading || _nextToggle > 0 || _spotSuspended > 0 ) return;
		LaserEnabled = !LaserEnabled;
		_nextToggle = SecondaryDelay;
		OnLaserToggled();
	}

	internal virtual void OnLaserToggled() { }

	internal virtual float LaserReloadDelay => 2.1f;

	protected override void OnReloadStarted()
	{
		base.OnReloadStarted();
		if ( LaserEnabled ) _spotSuspended = LaserReloadDelay;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( !IsActive || !Owner.IsValid() || !LaserEnabled || IsReloading || _spotSuspended > 0 )
		{
			SpotPosition = null;
			if ( _dot.IsValid() ) _dot.Enabled = false;
			return;
		}

		var sight = Owner.EyeTransform.ForwardRay;
		var hit = BulletTrace( sight, 8192, 0 ).Run();
		SpotPosition = hit.EndPosition;
		if ( Application.IsDedicatedServer ) return;
		if ( !_dot.IsValid() ) _dot = LaserSpot.Create( GameObject, ContentMount, "RPG laser spot" );
		if ( !_dot.IsValid() ) return;
		_dot.Enabled = hit.Hit;
		_dot.WorldPosition = hit.EndPosition - sight.Forward * 0.5f;
	}
}

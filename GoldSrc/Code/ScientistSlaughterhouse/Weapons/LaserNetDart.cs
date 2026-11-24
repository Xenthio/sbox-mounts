using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

[Hide]
public sealed class LaserNetDart : CrossbowDart
{
	static readonly Color NetColor = new Color32( 255, 10, 10, 255 );
	const float LinkRange = 250;
	const float AnchorLife = 10;

	protected override Color TrailColor => new Color32( 255, 12, 12, 255 );
	internal override float PlayerDamage => 5;

	internal override void HandleContact( SceneTraceResult hit )
	{
		base.HandleContact( hit );
		if ( Enabled ) return;

		var anchor = GameObject.AddComponent<LaserNetAnchor>();
		anchor.TimeLeft = AnchorLife;
		var start = WorldPosition;
		var remainingReach = LinkRange;
		foreach ( var other in Scene.GetAllComponents<LaserNetAnchor>() )
		{
			if ( other == anchor || other.TimeLeft < 1 ) continue;
			var end = other.WorldPosition;
			var distance = start.Distance( end );
			if ( distance >= remainingReach ) continue;
			if ( Scene.Trace.Ray( start, end ).IgnoreGameObjectHierarchy( GameObject ).IgnoreGameObjectHierarchy( other.GameObject )
				.WithoutTags( "npc", "player", "playercontroller", "trigger" ).Run().Hit ) continue;

			var link = new GameObject( false, "Laser net link" ) { WorldPosition = start, NetworkMode = NetworkMode.Never };
			var damage = link.AddComponent<LaserNetLink>();
			damage.Initialize( anchor, other, Shooter );
			ShowLink( start, end, other.GameObject, other.TimeLeft );
			link.Enabled = true;

			// Long links spend some of the new anchor's remaining reach.
			if ( distance >= remainingReach * 0.5f ) remainingReach -= distance;
		}
		ShowAnchor( start );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowLink( Vector3 start, Vector3 end, GameObject other, float duration )
	{
		if ( Application.IsDedicatedServer || !other.IsValid() ) return;
		var beam = EnergyBeam.Show( start, end, NetColor, "lgtning", "Laser net beam", 2, duration, ModelMount );
		var endpoints = beam.AddComponent<LaserNetVisual>();
		endpoints.Start = GameObject;
		endpoints.End = other;
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowAnchor( Vector3 position )
	{
		if ( Application.IsDedicatedServer || ModelMount is not { } mount ) return;
		var glow = Blast.SpriteEffect( "Laser net anchor glow", "glow01", position, 0.15f, 10, mount: mount );
		if ( !glow.IsValid() ) return;
		glow.GetComponent<SpriteRenderer>().Color = NetColor;
		glow.GetComponent<TemporaryEffect>().DestroyAfterSeconds = AnchorLife;
	}
}

[Hide]
public sealed class LaserNetAnchor : Component
{
	internal TimeUntil TimeLeft { get; set; }
}

[Hide]
public sealed class LaserNetLink : Component
{
	LaserNetAnchor _start, _end;
	GameObject _shooter;

	internal void Initialize( LaserNetAnchor start, LaserNetAnchor end, GameObject shooter )
	{
		_start = start;
		_end = end;
		_shooter = shooter;
	}

	protected override void OnFixedUpdate()
	{
		if ( !_start.IsValid() || !_end.IsValid() || _start.TimeLeft <= 0 || _end.TimeLeft <= 0 )
		{
			GameObject.Destroy();
			return;
		}

		var hit = Scene.Trace.Ray( WorldPosition, _end.WorldPosition ).IgnoreGameObjectHierarchy( _start.GameObject )
			.IgnoreGameObjectHierarchy( _end.GameObject ).WithoutTags( "trigger", "playercontroller" ).UseHitboxes().Run();
		if ( hit.GameObject?.GetComponentInParent<IDamageable>() is not { } victim ) return;

		var damage = new DamageInfo
		{
			Attacker = _shooter,
			Weapon = GameObject,
			Damage = 10 * Time.Delta,
			Origin = WorldPosition,
			Position = hit.EndPosition,
			Hitbox = hit.Hitbox
		};
		damage.Tags.Add( "energybeam" );
		victim.OnDamage( damage );
	}
}

[Hide]
public sealed class LaserNetVisual : Component
{
	internal GameObject Start { get; set; }
	internal GameObject End { get; set; }

	protected override void OnUpdate()
	{
		if ( !Start.IsValid() || !End.IsValid() ) GameObject.Destroy();
	}
}

using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

[Hide]
public sealed class RepulsorPulse : NpcProjectile
{
	int _contacts;
	readonly HashSet<GameObject> _touched = [];

	internal override string HitTag => "fall";
	internal override void PrepareLaunch() => _contacts = Game.Random.Int( 4, 8 );

	protected override void OnStart()
	{
		if ( Application.IsDedicatedServer || ModelMount is not { } mount ) return;
		if ( mount.GetIndexedSprite( "xspark4" ) is { } sprite )
		{
			var glow = GameObject.AddComponent<SpriteRenderer>();
			glow.Sprite = sprite;
			var frame = sprite.Animations[0].Frames[0].Texture;
			glow.Size = new Vector2( frame.Width, frame.Height );
			glow.PlaybackSpeed = 0;
			glow.Color = new Color( 0, 1, 0 );
			glow.Additive = true;
		}
		ProjectileTrail.Attach( GameObject, mount, "Repulsor trail", "xenobeam", 5, 0.5f, new Color( 0, 1, 0 ) );
	}

	internal override SceneTrace FlightTrace( Vector3 start, Vector3 end )
	{
		var trace = base.FlightTrace( start, end ).WithoutTags( "trigger", "playercontroller" );
		foreach ( var obj in _touched )
			if ( obj.IsValid() ) trace = trace.IgnoreGameObjectHierarchy( obj );
		return trace;
	}

	internal override void HandleContact( SceneTraceResult hit )
	{
		var damageable = hit.GameObject?.GetComponentInParent<IDamageable>();
		if ( Damage > 0 && damageable is not null )
		{
			var damage = new DamageInfo( Damage, Shooter, GameObject ) { Position = hit.EndPosition, Origin = WorldPosition };
			damage.Tags.Add( "fall" );
			damage.Tags.Add( "nevergib" );
			damageable.OnDamage( damage );
		}

		var target = RepulsorGun.GetMovable( hit.GameObject );
		if ( target.IsValid() ) RepulsorGun.GiveVelocity( target, Velocity );
		if ( target.IsValid() || (Damage > 0 && damageable is not null) || --_contacts <= 0 || !hit.GameObject.IsValid() )
		{
			GameObject.Destroy();
			return;
		}

		// Non-damageable solids consume contacts. Do not tunnel through fixed world geometry.
		if ( hit.Body?.BodyType != PhysicsBodyType.Dynamic ) return;
		_touched.Add( hit.GameObject );
		WorldPosition = hit.EndPosition + Velocity.Normal * 0.1f;
	}
}

using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

[Hide]
public class LaserPulse : NpcProjectile
{
	protected static readonly Color PulseColor = Color.Red;
	protected SpriteRenderer Glow { get; private set; }
	internal override string HitTag => "energybeam";

	protected override void OnStart()
	{
		if ( Application.IsDedicatedServer || ModelMount is not { } mount ) return;
		if ( mount.GetIndexedSprite( "glow01" ) is { } sprite )
		{
			var image = sprite.Animations[0].Frames[0].Texture;
			Glow = GameObject.AddComponent<SpriteRenderer>();
			Glow.Sprite = sprite;
			Glow.Size = new Vector2( image.Width, image.Height ) * 0.25f;
			Glow.Additive = true;
			Glow.Color = PulseColor;
		}
		ProjectileTrail.Attach( GameObject, mount, "Pulse laser trail", "lred", 2, 1, PulseColor );
	}

	internal override SceneTrace FlightTrace( Vector3 start, Vector3 end ) => base.FlightTrace( start, end ).WithoutTags( "trigger" );

	internal override void HandleContact( SceneTraceResult hit )
	{
		var victim = hit.GameObject?.GetComponentInParent<IDamageable>();
		var impact = new DamageInfo { Attacker = Shooter, Weapon = GameObject, Origin = WorldPosition,
			Position = hit.EndPosition, Normal = hit.Normal, Hitbox = hit.Hitbox, Damage = Damage };
		impact.Tags.Add( HitTag );
		impact.Tags.Add( "nevergib" );
		victim?.OnDamage( impact );
		if ( victim is null && !WaterBody.Find( Scene, hit.EndPosition ).IsValid() ) ShowSparks( hit.EndPosition );
		GameObject.Destroy();
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowSparks( Vector3 position )
	{
		if ( !Application.IsDedicatedServer ) StudioSparks.Emit( position, Game.Random.Int( 0, 4 ) );
	}
}

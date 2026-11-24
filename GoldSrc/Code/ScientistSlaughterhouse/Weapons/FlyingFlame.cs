using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

[Hide]
public class FlyingFlame : NpcProjectile
{
	internal virtual bool Acid => false;
	internal override float Lifetime => 3;
	internal override string HitTag => Acid ? "acid" : "slowburn";
	TimeUntil _nextBurn = 1;
	SpriteRenderer _sprite;
	Vector2 _spriteSize;

	protected override void OnStart()
	{
		if ( Application.IsDedicatedServer || ModelMount?.GetIndexedSprite( Acid ? "bio_blast" : "flamer" ) is not { } sprite ) return;
		var animation = sprite.Animations[0];
		var image = animation.Frames[0].Texture;
		_spriteSize = new Vector2( image.Width, image.Height );
		_sprite = GameObject.AddComponent<SpriteRenderer>();
		_sprite.Sprite = sprite;
		_sprite.Size = _spriteSize;
		_sprite.Additive = true;
		_sprite.PlaybackSpeed = 10 / animation.FrameRate;
	}

	protected override void OnUpdate()
	{
		if ( !_sprite.IsValid() ) return;
		float age = Age;
		_sprite.Size = _spriteSize * (Acid ? 0.1f + age * 0.1f : 0.5f + age * 0.5f);
		_sprite.Color = Color.White.WithAlpha( (1 - age / Lifetime).Clamp( 0, 1 ) );
	}

	internal override SceneTrace FlightTrace( Vector3 start, Vector3 end ) => base.FlightTrace( start, end ).WithoutTags( "trigger" );

	internal override void Guide()
	{
		if ( Acid ) Velocity += Scene.PhysicsWorld.Gravity * (0.1f * Time.Delta);
		if ( _nextBurn > 0 ) return;
		_nextBurn = 0.1f;
		Velocity *= 0.8f;
		var radius = 10 + System.MathF.Max( (float)Age - 1, 0 ) * 20;
		Blast.ApplyRadiusDamage( GameObject, Shooter, WorldPosition, Damage, radius, ignoreAttacker: true, HitTag, "nevergib" );
	}

	internal override void HandleContact( SceneTraceResult hit )
	{
		if ( hit.GameObject?.GetComponentInParent<IDamageable>() is { } victim )
		{
			var info = new DamageInfo { Attacker = Shooter, Weapon = GameObject, Origin = WorldPosition,
				Position = hit.EndPosition, Normal = hit.Normal, Hitbox = hit.Hitbox, Damage = Damage };
			info.Tags.Add( HitTag );
			info.Tags.Add( "nevergib" );
			victim.OnDamage( info );
		}
		else if ( hit.GameObject?.GetComponent<Rigidbody>() is not { MotionEnabled: true } )
			ShowDecal( hit.EndPosition, hit.Normal, Acid ? $"spit{Game.Random.Int( 1, 2 )}" : $"smscorch{Game.Random.Int( 1, 3 )}" );
		GameObject.Destroy();
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowDecal( Vector3 position, Vector3 normal, string name ) => ImpactDecal.Place( position, normal, name, 0, ModelMount );
}

[Hide]
public sealed class AcidFlame : FlyingFlame
{
	internal override bool Acid => true;
}

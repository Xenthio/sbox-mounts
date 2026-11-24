using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

[Hide]
public sealed class FreezeSpark : NpcProjectile
{
	// Clear missed shots on open Sandbox maps without an enclosing world hull.
	internal override float Lifetime => 12;
	internal override string HitTag => "slowfreeze";
	int _ricochetsLeft;
	SpriteRenderer _sprite;
	TimeUntil _nextFrame;

	internal override void PrepareLaunch() => _ricochetsLeft = Game.Random.Int( 3, 6 );

	protected override void OnStart()
	{
		if ( Application.IsDedicatedServer || ModelMount?.GetIndexedSprite( "qtorp" ) is not { } sprite ) return;
		var image = sprite.Animations[0].Frames[0].Texture;
		_sprite = GameObject.AddComponent<SpriteRenderer>();
		_sprite.Sprite = sprite;
		_sprite.Size = new Vector2( image.Width, image.Height ) * 0.8f;
		_sprite.PlaybackSpeed = 0;
		_sprite.Additive = true;
		_nextFrame = 0.1f;
	}

	protected override void OnUpdate()
	{
		if ( !_sprite.IsValid() || _nextFrame > 0 ) return;
		_nextFrame = 0.1f;
		_sprite.CurrentFrameIndex = Game.Random.Int( 0, _sprite.CurrentAnimation.Frames.Count - 1 );
	}

	internal override SceneTrace FlightTrace( Vector3 start, Vector3 end ) =>
		Scene.Trace.Box( BBox.FromPositionAndSize( Vector3.Zero, Vector3.One * 2 ), start, end ).WithoutTags( "trigger" );

	internal override void HandleContact( SceneTraceResult hit )
	{
		if ( hit.GameObject?.GetComponentInParent<IDamageable>() is { } victim )
		{
			var damage = new DamageInfo { Attacker = Shooter, Weapon = GameObject, Origin = WorldPosition,
				Position = hit.EndPosition, Normal = hit.Normal, Damage = Damage };
			damage.Tags.Add( HitTag );
			damage.Tags.Add( "nevergib" );
			victim.OnDamage( damage );
		}
		else if ( !hit.StartedSolid && _ricochetsLeft > 0 )
		{
			_ricochetsLeft--;
			WorldPosition = hit.EndPosition + hit.Normal * 0.25f;
			Velocity = Vector3.Reflect( Velocity, hit.Normal );
			WorldRotation = Rotation.LookAt( Velocity );
			return;
		}

		ShowImpact( hit.EndPosition, _sprite.IsValid() ? _sprite.CurrentFrameIndex : 0 );
		GameObject.Destroy();
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowImpact( Vector3 position, int frame )
	{
		if ( Application.IsDedicatedServer || ModelMount is not { } mount ) return;
		NpcAudio.Play( mount.NpcAudio.Find( "x/x_shoot1.wav" ), position, attenuation: 0.8f );
		if ( Blast.SpriteEffect( "Freeze Spark impact", "qtorp", position, 0.8f, 10, mount: mount ) is not { } effect ) return;
		var sprite = effect.GetComponent<SpriteRenderer>();
		sprite.PlaybackSpeed = 0;
		sprite.CurrentFrameIndex = frame;
		effect.AddComponent<FreezeImpactFade>().Sprite = sprite;
		effect.GetComponent<TemporaryEffect>().DestroyAfterSeconds = FreezeImpactFade.Duration;
	}
}

[Hide]
public sealed class FreezeImpactFade : Component
{
	internal const float Duration = 255f / 500;
	internal SpriteRenderer Sprite { get; set; }
	Vector2 _baseSize;
	TimeSince _age;

	protected override void OnStart()
	{
		_baseSize = Sprite.Size / 0.8f;
		_age = 0;
	}

	protected override void OnUpdate()
	{
		float age = _age;
		Sprite.Color = Color.White.WithAlpha( (1 - age / Duration).Clamp( 0, 1 ) );
		Sprite.Size = _baseSize * (0.8f + age * 4);
	}
}

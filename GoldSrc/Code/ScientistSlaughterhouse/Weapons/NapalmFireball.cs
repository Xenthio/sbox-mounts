using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

[Hide]
public class NapalmFireball : NpcProjectile
{
	internal override float Lifetime => 12;
	internal virtual bool Ballistic => false;
	TimeUntil _nextTrail = 0.05f;
	int _trailSprites = 10;
	bool _landed, _detonated;

	protected override void OnStart()
	{
		if ( Application.IsDedicatedServer || ModelMount?.GetIndexedSprite( "fireball" ) is not { } sprite ) return;
		var animation = sprite.Animations[0];
		var image = animation.Frames[0].Texture;
		var renderer = GameObject.AddComponent<SpriteRenderer>();
		renderer.Sprite = sprite;
		renderer.Size = new Vector2( image.Width, image.Height );
		renderer.PlaybackSpeed = 10 / animation.FrameRate;
		renderer.Additive = true;
		var light = GameObject.AddComponent<PointLight>();
		light.Radius = 320;
		light.LightColor = new Color32( 255, 192, 0, 255 );
		light.Shadows = false;
	}

	protected override void OnUpdate()
	{
		if ( Application.IsDedicatedServer || _trailSprites == 0 || _nextTrail > 0 || ModelMount is not { } mount ) return;
		_nextTrail = 0.05f;
		var puff = Blast.SpriteEffect( "Napalm trail", "fireball", WorldPosition, _trailSprites * 0.1f, 10, mount: mount );
		if ( puff.IsValid() ) puff.GetComponent<TemporaryEffect>().DestroyAfterSeconds = _trailSprites * 0.05f;
		_trailSprites--;
	}

	internal override void Guide()
	{
		if ( _detonated ) return;
		if ( WaterBody.Find( Scene, WorldPosition ).IsValid() )
		{
			_detonated = true;
			ShowExtinguish( WorldPosition );
			GameObject.Destroy();
			return;
		}
		if ( Ballistic ) Velocity += Scene.PhysicsWorld.Gravity * Time.Delta;
	}

	internal override SceneTrace FlightTrace( Vector3 start, Vector3 end ) =>
		Scene.Trace.Box( BBox.FromPositionAndSize( Vector3.Zero, Vector3.One * 8 ), start, end ).WithoutTags( "trigger" );

	internal override bool Bounce( SceneTraceResult hit )
	{
		if ( !Ballistic || _landed || hit.Normal.z < 0.7f ) return false;
		_landed = true;
		WorldPosition = hit.EndPosition + hit.Normal * 0.25f;
		Velocity = Vector3.Down * System.MathF.Abs( Velocity.z );
		return true;
	}

	internal override void OnImpact( IDamageable victim, Vector3 position, Vector3 normal ) =>
		Detonate( position, normal, WaterBody.Find( Scene, position ).IsValid() );

	void Detonate( Vector3 contact, Vector3 normal, bool underwater )
	{
		if ( _detonated ) return;
		_detonated = true;
		if ( underwater )
		{
			Blast.Detonate( GameObject, Shooter, contact, normal, Damage * 0.5f, true, ModelMount );
		}
		else
		{
			var center = contact + normal * ((Damage - 24) * 0.6f);
			Blast.DamageInRadius( GameObject, Shooter, center, Damage, Damage * 2.5f, "burn", "nevergib" );
			ImpactDecal.Place( contact, normal, $"scorch{Game.Random.Int( 1, 2 )}", 48, ModelMount );
			ModelMount?.NpcAudio.Emit( $"weapons/explode{Game.Random.Int( 3, 5 )}.wav", center, attenuation: 0.3f );
			ModelMount?.NpcAudio.Emit( "weapons/debris", center, volume: 0.55f );
			ShowBlast( center, Damage );
		}
		GameObject.Destroy();
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowExtinguish( Vector3 position )
	{
		if ( !Application.IsDedicatedServer ) Blast.Bubbles( position, 64, 100, "Extinguished napalm bubbles", ModelMount );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	async void ShowBlast( Vector3 center, float damage )
	{
		if ( Application.IsDedicatedServer || ModelMount is not { } mount ) return;
		var scene = Scene;
		var scale = ((damage - 50) * 0.06f).Clamp( 1, 25.5f );
		Blast.SpriteEffect( "Napalm explosion", "fexplo", center, scale, 15, mount: mount );
		await GameTask.DelaySeconds( 0.3f );
		if ( scene.IsValid() ) Blast.SmokePuff( "Napalm smoke", center, ((damage - 50) * 0.08f).Clamp( 1, 25.5f ), mount );
	}
}

[Hide]
public sealed class BallisticFireball : NapalmFireball
{
	internal override bool Ballistic => true;
}

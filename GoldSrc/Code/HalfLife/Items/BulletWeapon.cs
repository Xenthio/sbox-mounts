namespace GoldSrc.HalfLife;

[Hide]
public abstract class BulletWeapon : StudioWeapon
{
	internal abstract Vector2 PelletSpread { get; }
	internal override float? IdleAfterShot => Game.Random.Float( 10, 15 );
	// Muzzle event option from the matching player.mdl shoot sequence.
	internal abstract int PlayerMuzzleFlash { get; }
	internal static bool Deathmatch => Networking.MaxPlayers > 1;

	protected override void OnShootEffects( ShotEffect shot )
	{
		base.OnShootEffects( shot );
		if ( !Application.IsDedicatedServer && (shot.Hit || shot.HitPosition != default) )
			Blast.BulletBubbles( Scene, GetMuzzleTransform().Position, shot.HitPosition );
		if ( !shot.NoEvents && PlayerMuzzleFlash >= 0 ) ShowPlayerMuzzleFlash();
	}

	protected void ShowPlayerMuzzleFlash()
	{
		if ( Application.IsDedicatedServer || (!IsProxy && Owner is { ThirdPerson: false }) ) return;
		if ( WorldModel?.GetComponent<SkinnedModelRenderer>() is { } renderer )
			StudioSequenceEvents.ShowMuzzleFlash( ContentMount, renderer, GetMuzzleTransform(), PlayerMuzzleFlash );
	}

	// FireBulletsPlayer applies its spread at trace time, independently for each pellet.
	public sealed override Vector2 CurrentSpread => Vector2.Zero;

	protected sealed override SceneTrace BulletTrace( Ray ray, float distance, float radius )
	{
		var spread = PelletSpread;
		var basis = Rotation.LookAt( ray.Forward );
		var offset = SamplePelletOffset();
		var direction = ray.Forward + basis.Right * (offset.x * spread.x) + basis.Up * (offset.y * spread.y);
		return base.BulletTrace( new Ray( ray.Position, direction.Normal ), distance * direction.Length, radius );
	}

	internal virtual Vector2 SamplePelletOffset() => new( SampleOffset(), SampleOffset() );

	static float SampleOffset() => Game.Random.Float( -0.5f, 0.5f ) + Game.Random.Float( -0.5f, 0.5f );
}

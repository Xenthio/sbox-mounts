using System;
using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Hide]
public abstract class CounterStrikeFirearm : StudioWeapon
{
	internal abstract float RangeModifier { get; }
	internal virtual float ShotDamage => Ballistics.Damage;

	protected void FireRound( float spread, float? damage = null, float? rangeModifier = null )
	{
		var ray = AimRay;
		var basis = Rotation.LookAt( ray.Forward );
		var offset = basis.Right * (SampleOffset() * spread) + basis.Up * (SampleOffset() * spread);
		var trace = BulletTrace( new Ray( ray.Position, (ray.Forward + offset).Normal ), Ballistics.Range, 0 ).Run();
		if ( trace.Hit )
			ShootBullet( trace, (damage ?? ShotDamage) * MathF.Pow( rangeModifier ?? RangeModifier, trace.Distance / 500f ), 0 );

		TimeSinceShoot = 0;
		ShootEffects( new ShotEffect( trace.EndPosition, trace.Hit, trace.Normal, trace.GameObject, trace.Surface ) );
	}

	static float SampleOffset() => Game.Random.Float( -0.5f, 0.5f ) + Game.Random.Float( -0.5f, 0.5f );
}

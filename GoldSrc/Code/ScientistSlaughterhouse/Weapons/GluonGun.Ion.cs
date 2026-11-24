using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

public sealed partial class GluonGun
{
	const int MaxIonBeams = 5;
	const float IonDamage = 6;
	static readonly float[] IonAngles = [0, -15, 15, -30, 30];
	static readonly TagSet IonTags = ["shock", "nevergib"];

	readonly (GameObject Object, Vector3 LocalPoint)[] _ionTargets = new (GameObject, Vector3)[MaxIonBeams];
	IonDischargeBeam _ionVisual;
	TimeUntil _nextIonLevel;
	int _lastIonLevel;

	[Sync( SyncFlags.FromHost )] int IonLevel { get; set; }

	public override bool CanSecondaryAttack() => Mode == EgonMode.Ion && base.CanSecondaryAttack();

	public override void SecondaryAttack() => CycleIonLevel();

	[Rpc.Host( NetFlags.OwnerOnly )]
	void CycleIonLevel()
	{
		if ( !IsActive || !Owner.IsValid() || Mode != EgonMode.Ion || _nextIonLevel > 0 ) return;
		_nextIonLevel = SecondaryDelay;
		IonLevel = (IonLevel + 1) % 3;
	}

	protected override void ShootBeam( bool start )
	{
		if ( Mode != EgonMode.Ion )
		{
			base.ShootBeam( start );
			return;
		}

		if ( start || _lastIonLevel != IonLevel )
		{
			_lastIonLevel = IonLevel;
			System.Array.Clear( _ionTargets );
		}
		var ray = AimRay;
		var endpoints = new Vector3[1 + 2 * IonLevel];
		SceneTraceResult center = default;
		for ( int i = 0; i < endpoints.Length; i++ )
		{
			var direction = Rotation.FromYaw( IonAngles[i] ) * ray.Forward;
			var (target, offset) = _ionTargets[i];
			if ( target.IsValid() )
			{
				var locked = target.WorldTransform.PointToWorld( offset ) - ray.Position;
				if ( direction.Dot( locked ) >= 0 ) direction = locked.Normal;
			}

			var hit = BulletTrace( new Ray( ray.Position, direction ), Ballistics.Range, 0 )
				.WithoutTags( "trigger" ).UseHitboxes().Run();
			if ( i == 0 ) center = hit;
			endpoints[i] = hit.EndPosition;
			_ionTargets[i] = default;
			if ( start || !hit.Hit || hit.StartedSolid || hit.GameObject?.GetComponentInParent<IDamageable>() is null ) continue;

			ShootBullet( hit, IonDamage, 0, IonTags );
			// Each stream follows its own impact point as the target moves.
			_ionTargets[i] = (hit.GameObject, hit.GameObject.WorldTransform.PointToLocal( hit.EndPosition ));
		}

		if ( !start ) SpendBeamCell();
		ShowBeamShot( center, start );
		ShowIonBeams( endpoints );
	}

	internal override void DrawShotTrail( Vector3 start, Vector3 end )
	{
		if ( Mode != EgonMode.Ion ) base.DrawShotTrail( start, end );
	}

	[Rpc.Broadcast( NetFlags.OwnerOnly )]
	void ShowIonBeams( Vector3[] endpoints )
	{
		if ( Application.IsDedicatedServer || endpoints.Length is not (1 or 3 or MaxIonBeams) ) return;
		base.StopBeamVisual();
		if ( !_ionVisual.IsValid() ) _ionVisual = IonDischargeBeam.Start( this );
		_ionVisual?.Aim( endpoints );
	}

	protected override void StopBeamVisual()
	{
		base.StopBeamVisual();
		if ( _ionVisual.IsValid() ) _ionVisual.GameObject.Destroy();
		_ionVisual = null;
		System.Array.Clear( _ionTargets );
	}
}

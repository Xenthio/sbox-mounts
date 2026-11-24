namespace GoldSrc.HalfLife;

public abstract partial class ClassicGaussGun
{
	void FireBeam( float damage, bool charged )
	{
		var ray = AimRay;
		var origin = ray.Position;
		var direction = ray.Forward;
		var ignore = Owner.GameObject;
		bool punched = false;
		_aftershock = Game.Random.Float( 0.3f, 0.8f );
		ShowFire( damage );
		var recoil = charged ? -direction * damage * 5 : Vector3.Zero;
		if ( Networking.MaxPlayers <= 1 ) recoil.z = 0;
		GoldSrc.HalfLife.Npc.PunchPlayer( Owner, recoil, new Angles( -2, 0, 0 ) );

		for ( int i = 0; i < 10 && damage > 10; ++i )
		{
			var trace = Scene.Trace.Ray( origin, origin + direction * 8192 ).WithCollisionRules( "bullet" ).WithoutTags( "trigger", "playercontroller" ).UseHitboxes();
			if ( ignore.IsValid() ) trace = trace.IgnoreGameObjectHierarchy( ignore );
			var hit = trace.Run();
			if ( hit.StartedSolid ) break;
			ShowSegment( origin, hit.EndPosition, charged, damage );
			if ( !hit.Hit ) break;

			var victim = hit.GameObject?.GetComponentInParent<IDamageable>() as Component;
			if ( victim.IsValid() || hit.Body?.BodyType == PhysicsBodyType.Dynamic )
			{
				ignore = victim.IsValid() ? victim.GameObject : hit.GameObject;
				if ( victim.IsValid() ) ShootBullet( hit, damage, 0, AttackDamageTags );
				origin = hit.EndPosition + direction;
				continue;
			}
			ignore = null;
			var incidence = -hit.Normal.Dot( direction );
			if ( incidence < 0.5f )
			{
				ShowReflection( hit.EndPosition, hit.Normal, damage * incidence );
				direction += hit.Normal * (2 * incidence);
				origin = hit.EndPosition + direction * 8;
				if ( incidence > 0 ) Blast.DamageInRadius( GameObject, Owner.GameObject, hit.EndPosition, damage * incidence, damage * incidence * 2.5f, "blast" );
				damage *= 1 - (incidence > 0 ? incidence : 0.1f);
				continue;
			}
			ShowWallHit( hit.EndPosition, hit.Normal, damage, !charged );
			if ( !charged || punched ) break;
			punched = true;

			// Look back from the furthest point the remaining charge can penetrate.
			var exit = Scene.Trace.Ray( hit.EndPosition + direction * damage, hit.EndPosition - direction )
				.WithCollisionRules( "bullet" ).WithoutTags( "playercontroller", "trigger" ).RunAll().FirstOrDefault( result => result.Collider == hit.Collider && result.GameObject == hit.GameObject );
			if ( exit.StartedSolid || !exit.Hit ) break;
			damage -= System.MathF.Max( 1, exit.EndPosition.Distance( hit.EndPosition ) );
			if ( damage <= 0 ) break;
			ShowPenetration( hit.EndPosition, exit.EndPosition, exit.Normal, direction, damage );
			var radius = damage * (Networking.MaxPlayers > 1 ? 1.75f : 2.5f);
			Blast.DamageInRadius( GameObject, Owner.GameObject, exit.EndPosition + direction * 8, damage, radius, "blast" );
			origin = exit.EndPosition + direction;
		}
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowFire( float damage )
	{
		StopSpin();
		PlaySample( "weapons/gauss2.wav", 0.5f + damage / 400, Game.Random.Float( 0.85f, 1.16f ) );
		PostWeaponEvent( WeaponEvents.Attack );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( ShotSequence );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowSegment( Vector3 start, Vector3 end, bool charged, float damage ) =>
		EnergyBeam.Show( start, end, charged ? Color.White.WithAlpha( damage / 255 ) : new Color( 1, 0.5f, 0, 0.5f ), "smoke", "Gauss beam", charged ? 2.5f : 1, 0.1f, ContentMount );

}

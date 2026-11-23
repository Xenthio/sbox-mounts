namespace GoldSrc.HalfLife;

public partial class Npc
{
	internal sealed record SavedMagazine( int Rounds, float ShotDelay );

	internal virtual SavedMagazine CaptureMagazine() => null;
	internal virtual void RestoreMagazine( SavedMagazine magazine ) { }

	// GoldSrc shares tracer cadence across NPCs firing in the same game.
	static uint _tracerCount;

	internal void PlayGunshot( string sample, Vector3 origin, float volume = 1, float pitch = 1, float attenuation = GoldSrcSoundEvents.Normal )
	{
		EmitSound( sample, origin, volume, pitch, attenuation: attenuation );
		EmitStimulus( Sandbox.Npcs.StimulusKinds.Gunshot, radius: 384, lifetime: 0.3f );
	}

	internal void TraceBullets( Vector3 origin, Vector3 end, int count, float amount, float spread, float range, int tracerFrequency = 4 )
	{
		var aim = Rotation.LookAt( end - origin );
		for ( int i = 0; i < count; ++i )
		{
			float x, y;
			do
			{
				x = Game.Random.Float( -0.5f, 0.5f ) + Game.Random.Float( -0.5f, 0.5f );
				y = Game.Random.Float( -0.5f, 0.5f ) + Game.Random.Float( -0.5f, 0.5f );
			} while ( x * x + y * y > 1 );
			var direction = aim.Forward + spread * (aim.Right * x + aim.Up * y);
			var trace = Scene.Trace.Ray( origin, origin + direction * range ).IgnoreGameObjectHierarchy( GameObject )
				.WithCollisionRules( "bullet" ).WithoutTags( "playercontroller" ).UseHitboxes().Run();
			if ( tracerFrequency > 0 && _tracerCount++ % tracerFrequency == 0 )
				BulletEffects.Tracer( origin, trace.EndPosition );
			if ( !trace.Hit ) continue;

			var victim = trace.GameObject?.GetComponentInParent<Component.IDamageable>();
			var flesh = trace.GameObject.IsValid() && ClassOf( trace.GameObject ) is not (MonsterClass.None or MonsterClass.Machine);
			BulletEffects.Impact( trace, flesh, victim is null );
			if ( victim is null ) continue;
			var damage = new DamageInfo { Damage = amount, Hitbox = trace.Hitbox, Origin = origin, Position = trace.EndPosition, Attacker = GameObject, Weapon = GameObject };
			damage.Tags.Add( "bullet" );
			damage.Tags.Add( "nevergib" );
			victim.OnDamage( damage );
		}
	}
}

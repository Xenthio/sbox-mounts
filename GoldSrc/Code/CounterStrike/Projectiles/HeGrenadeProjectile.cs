using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Hide]
public sealed partial class HeGrenadeProjectile : CounterStrikeTimedGrenade
{
	internal override float GravityScale => 0.55f;
	internal override float Friction => 0.7f;

	internal static void Throw( GameObject owner, Vector3 origin, Vector3 velocity, GameMount mount ) =>
		ThrowTimed<HeGrenadeProjectile>( owner, origin, velocity, mount, "HE grenade", "w_hegrenade" );

	internal override void PlayBounceSound() =>
		ContentMount?.NpcAudio.Emit( "weapons/he_bounce-1.wav", WorldPosition, volume: 0.25f );

	internal override void Detonate()
	{
		const float damage = 100;
		const float radius = damage * 3.5f;
		var floor = TraceFloor();
		var origin = floor.Hit ? floor.EndPosition + floor.Normal * ((damage - 24) * 0.6f) : WorldPosition;
		ApplyBlastDamage( origin, damage, radius );
		ShowExplosion( MountId, origin, floor.EndPosition, floor.Normal, floor.Hit );
		ContentMount?.NpcAudio.Emit( "weapons/hegrenade-", origin );
		ContentMount?.NpcAudio.Emit( "weapons/debris", origin, volume: 0.55f );
	}

	void ApplyBlastDamage( Vector3 origin, float damage, float radius )
	{
		Scene.GetSystem<Sandbox.Npcs.NpcSystem>()?.EmitStimulus( "explosion", origin, GameObject, 1024, 1 );
		var submerged = WaterBody.Find( Scene, origin ).IsValid();
		origin += Vector3.Up;
		var checkCover = MountId == "czero";
		var victims = Scene.FindInPhysics( new Sphere( origin, radius ) )
			.Select( item => item.GetComponentInParent<Component.IDamageable>() ).OfType<Component>().Distinct().ToArray();
		foreach ( var victim in victims )
		{
			if ( !victim.IsValid() || !Npc.SharesWaterLevel( victim.GameObject, submerged ) ) continue;
			var player = victim.GetComponentInParent<PlayerController>();
			var center = player.IsValid() ? player.WorldPosition + Vector3.Up * (player.IsDucking ? 18 : 36) : victim.WorldPosition;
			var falloff = 1 - origin.Distance( center ) / radius;
			if ( falloff <= 0 ) continue;

			// Counter-Strike HE damage passes through walls. Condition Zero weighs the visible portions of the target.
			var amount = damage * falloff;
			if ( checkCover ) amount *= falloff * 1.875f * VisibleFraction( origin, center, victim.GameObject, player );
			if ( amount <= 0 ) continue;
			var info = new DamageInfo( amount, Thrower, GameObject ) { Origin = origin, Position = center };
			info.Tags.Add( "blast" );
			((Component.IDamageable)victim).OnDamage( info );
		}
	}

	float VisibleFraction( Vector3 origin, Vector3 center, GameObject target, PlayerController player )
	{
		bool Visible( Vector3 point ) => !TraceCover( origin, point ).IgnoreGameObjectHierarchy( target ).Run().Hit;

		if ( !player.IsValid() ) return Visible( center ) ? 1 : 0;

		var across = Vector3.Up.Cross( (center - origin).WithZ( 0 ).Normal ) * 13;
		var feet = player.IsDucking ? 14 : 34;
		return (Visible( center ) ? 0.4f : 0)
			+ (Visible( center + Vector3.Up * 25 ) ? 0.2f : 0)
			+ (Visible( center - Vector3.Up * feet ) ? 0.2f : 0)
			+ (Visible( center + across ) ? 0.1f : 0)
			+ (Visible( center - across ) ? 0.1f : 0);
	}
}

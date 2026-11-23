using GoldSrc.HalfLife;

namespace GoldSrc.SvenCoop;

public sealed partial class Tor
{
	const int MaxGrunts = 3;
	const string GruntPrefab = "mount://svencoop/entities/npcs/monster_alien_grunt.prefab";
	static readonly BBox GruntHull = new( new Vector3( -32, -32, 1 ), new Vector3( 32, 32, 64 ) );
	readonly List<SummonedAlienGrunt> _summonedGrunts = [];
	Vector3? _summonCandidate;
	Vector3? _summonPoint;
	TimeUntil _summonCooldown;
	TimeUntil _summonAt;
	TimeUntil _summonDeadline;

	bool CanSummon => !_summonPoint.HasValue && _summonCooldown <= 0
		&& _summonedGrunts.Count( grunt => grunt.IsValid() && grunt.Health > 0 ) < MaxGrunts;

	Vector3? FindSummonPoint()
	{
		var start = WorldPosition + Vector3.Up * 66;
		for ( int attempt = 0; attempt < 8; attempt++ )
		{
			var angle = Rotation.FromYaw( Game.Random.Float( 0, 360 ) );
			var end = start + angle.Forward * 256 + Vector3.Up * Game.Random.Float( 32, 112 );
			var route = Scene.Trace.Box( GruntHull, start, end ).IgnoreGameObjectHierarchy( GameObject ).WithoutTags( "trigger" ).Run();
			if ( route.StartedSolid || route.Fraction < 0.5f ) continue;

			var point = route.EndPosition - (end - start).Normal * 40;
			if ( SummonPointClear( point ) ) return point;
		}
		return null;
	}

	bool SummonPointClear( Vector3 point )
	{
		var trace = Scene.Trace.Box( GruntHull, point, point + Vector3.Up ).WithoutTags( "trigger" ).Run();
		return !trace.StartedSolid && !trace.Hit;
	}

	void StartSummon()
	{
		if ( !CanSummon ) return;
		var point = _summonCandidate ?? FindSummonPoint();
		_summonCandidate = null;
		if ( point is not { } origin || !SummonPointClear( origin ) ) return;

		_summonPoint = origin;
		_summonCooldown = Game.Random.Float( 5, 10 );
		_summonAt = 2;
		_summonDeadline = 3;
		EmitSound( "tor/tor-summon.wav", WorldPosition );
		EmitSound( "debris/beamstart8.wav", origin );
		ShowBeam( WorldPosition + Vector3.Up * 48, origin, true );
		ShowPortal( origin );
	}

	void UpdateSummon()
	{
		if ( _summonPoint is not { } origin || _summonAt > 0 ) return;
		if ( !SummonPointClear( origin ) && _summonDeadline > 0 )
		{
			_summonAt = 0.5f;
			return;
		}

		_summonPoint = null;
		if ( _summonDeadline <= 0 ) return;
		var prefab = GameObject.GetPrefab( GruntPrefab );
		if ( !prefab.IsValid() ) return;
		var child = prefab.Clone( new Transform( origin, WorldRotation ) );
		child.NetworkSpawn( true, null );
		_summonedGrunts.RemoveAll( grunt => !grunt.IsValid() || grunt.Health <= 0 );
		_summonedGrunts.Add( child.GetComponent<SummonedAlienGrunt>() );
		EmitSound( "debris/beamstart7.wav", origin );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void ShowPortal( Vector3 origin )
	{
		var portal = Blast.SpriteEffect( "Tor summon portal", "exit1", origin, 2, 10, mount: SvenMount );
		if ( !portal.IsValid() ) return;
		portal.GetComponent<SpriteRenderer>().Color = Color.White.WithAlpha( 0.5f );
		portal.GetComponent<TemporaryEffect>().DestroyAfterSeconds = 2;
	}
}

[Hide]
[NpcDefinition( "monster_alien_grunt", "agrunt", MonsterClass.AlienMilitary, 150, 32, 64, 125,
	Mount = "svencoop", ShowInSpawnMenu = false, ArmorGroup = 10, Attack = NpcAttack.Projectile, Damage = 10,
	Range = 1024, ProjectileModel = "hornet", Gibs = GibSet.Alien, Blood = Npc.BloodColor.Yellow )]
public sealed class SummonedAlienGrunt : AlienGrunt;

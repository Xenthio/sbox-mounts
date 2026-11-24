using GoldSrc.HalfLife;
using Sandbox.Npcs;

namespace GoldSrc.ScientistSlaughterhouse;

[Hide]
public sealed class TeslaChain : Component
{
	const float SearchRange = 512;
	const int MaxJumps = 5;
	[Property] public GameObject Shooter { get; set; }
	[Property] public GameObject Weapon { get; set; }
	readonly HashSet<GameObject> _visited = [];
	GameObject _anchor, _previousAnchor, _olderAnchor;
	Vector3 _origin, _previousPoint, _olderPoint;
	TimeUntil _nextTick, _nextJump;
	int _jumps;

	internal static void Spawn( TeslaCoil weapon, Vector3 start, SceneTraceResult hit )
	{
		var go = new GameObject( false, "Tesla chain lightning" ) { WorldPosition = hit.EndPosition };
		var chain = go.AddComponent<TeslaChain>();
		chain.Shooter = weapon.Owner.GameObject;
		chain.Weapon = weapon.GameObject;
		chain._origin = start;
		chain._anchor = hit.GameObject?.GetComponentInParent<IDamageable>() is Component victim ? victim.GameObject : null;
		if ( chain._anchor.IsValid() ) chain._visited.Add( chain._anchor );
		chain._nextTick = 0.1f;
		chain._nextJump = 0.5f;
		go.Enabled = true;
		go.NetworkSpawn( true, null );
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost || _nextTick > 0 ) return;
		_nextTick = 0.1f;
		if ( !Shooter.IsValid() ) { GameObject.Destroy(); return; }
		if ( _nextJump <= 0 )
		{
			if ( _jumps == MaxJumps ) { GameObject.Destroy(); return; }
			Jump();
		}

		var point = _anchor.IsValid() ? Npc.TargetHullCenter( _anchor ) : WorldPosition;
		var start = _previousAnchor.IsValid() ? Npc.TargetHullCenter( _previousAnchor ) : _previousPoint;
		if ( _jumps == 0 ) start = _origin;
		Arc( start, point, _anchor, _previousAnchor );
		if ( _jumps > 0 )
			Arc( _olderPoint, start, _previousAnchor, _olderAnchor );
	}

	void Jump()
	{
		_nextJump = 0.5f;
		++_jumps;
		_olderPoint = _jumps == 1 ? _origin : _previousPoint;
		_olderAnchor = _previousAnchor;
		_previousAnchor = _anchor;
		_previousPoint = _anchor.IsValid() ? Npc.TargetHullCenter( _anchor ) : WorldPosition;
		_anchor = null;
		var nearest = SearchRange;
		foreach ( var obj in Scene.FindInPhysics( new Sphere( _previousPoint, SearchRange ) ) )
		{
			if ( obj.GetComponentInParent<IDamageable>() is not Component victim ) continue;
			var candidate = victim.GameObject;
			if ( candidate == Shooter || _visited.Contains( candidate ) || !BaseNpc.IsLiveTarget( candidate ) ) continue;
			var destination = Npc.TargetHullCenter( candidate );
			var distance = _previousPoint.Distance( destination );
			if ( distance >= nearest ) continue;
			var hit = Trace( _previousPoint, destination, _previousAnchor );
			if ( hit.Hit && hit.GameObject?.Root != candidate.Root ) continue;
			_anchor = candidate;
			nearest = distance;
		}

		if ( _anchor.IsValid() )
		{
			_visited.Add( _anchor );
			WorldPosition = Npc.TargetHullCenter( _anchor );
		}
		else
		{
			var direction = Vector3.Random.Normal;
			var rebound = Trace( _previousPoint, _previousPoint + direction * 1024, _previousAnchor );
			WorldPosition = rebound.EndPosition;
		}
		PlayArcSound( WorldPosition );
	}

	SceneTraceResult Trace( Vector3 start, Vector3 end, GameObject ignore )
	{
		var trace = Scene.Trace.Ray( start, end ).IgnoreGameObjectHierarchy( Shooter ).IgnoreGameObjectHierarchy( GameObject )
			.WithCollisionRules( "bullet" ).WithoutTags( "trigger", "playercontroller" ).UseHitboxes();
		if ( ignore.IsValid() ) trace = trace.IgnoreGameObjectHierarchy( ignore );
		return trace.Run();
	}

	void Arc( Vector3 start, Vector3 end, GameObject endpoint, GameObject ignore )
	{
		var hit = Trace( start, end, ignore );
		var target = hit.GameObject?.GetComponentInParent<IDamageable>() ?? (hit.Hit ? null : endpoint?.GetComponent<IDamageable>());
		if ( target is not null )
		{
			var damage = new DamageInfo( 5, Shooter, Weapon ) { Origin = start, Position = hit.EndPosition };
			damage.Tags.Add( "shock" );
			damage.Tags.Add( "nevergib" );
			target.OnDamage( damage );
		}
		ShowArc( start, hit.EndPosition );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowArc( Vector3 start, Vector3 end )
	{
		if ( !Application.IsDedicatedServer )
			EnergyBeam.Show( start, end, new Color( 0, 0.5f, 1 ), "lgtning", "Tesla chain arc", 8, 0.13f,
				Sandbox.Mounting.Directory.Get( "ssh" ) as GameMount );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void PlayArcSound( Vector3 point )
	{
		if ( Application.IsDedicatedServer || Sandbox.Mounting.Directory.Get( "ssh" ) is not GameMount mount ) return;
		NpcAudio.Play( mount.NpcAudio.Find( $"weapons/electro{Game.Random.Int( 4, 5 )}.wav" ), point, Game.Random.Float( 0.7f, 0.8f ) );
		NpcAudio.Play( mount.NpcAudio.Find( "weapons/electro6.wav" ), point, Game.Random.Float( 0.7f, 0.8f ) );
	}
}

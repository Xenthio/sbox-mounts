using GoldSrc.HalfLife;
using Sandbox.Npcs;

namespace GoldSrc.ScientistSlaughterhouse;

[Hide]
public sealed class EnergyDisk : NpcProjectile
{
	readonly Queue<GameObject> _recentHits = new();
	TimeUntil _nextGuide = 0.1f;
	SoundHandle _flight;

	internal override float Lifetime => 16;
	internal override string HitTag => "energybeam";

	protected override void OnStart()
	{
		base.OnStart();
		if ( !Application.IsDedicatedServer && ModelMount is { } mount )
			_flight = NpcAudio.Play( mount.NpcAudio.Find( "ambience/screammachine.wav" ), WorldPosition );
	}

	protected override void OnUpdate()
	{
		if ( _flight.IsValid() ) _flight.Position = WorldPosition;
	}

	protected override void OnDestroy() => _flight?.Stop();

	internal override void Guide()
	{
		if ( _nextGuide > 0 ) return;
		_nextGuide = 0.1f;
		if ( !BaseNpc.IsLiveTarget( Target ) )
			Target = ProjectileTarget.Find( Scene, Shooter, new Ray( WorldPosition, WorldRotation.Forward ), _recentHits );
		if ( Target.IsValid() )
		{
			var desired = Rotation.LookAt( Npc.TargetBodyPosition( Target ) - WorldPosition ).Angles();
			var current = WorldRotation.Angles();
			var delta = (desired - current).Normal;
			WorldRotation = new Angles( current.pitch + delta.pitch.Clamp( -10, 10 ), current.yaw + delta.yaw.Clamp( -10, 10 ), 0 );
		}
		Velocity = WorldRotation.Forward * 512;
	}

	internal override SceneTrace FlightTrace( Vector3 start, Vector3 end )
	{
		var trace = base.FlightTrace( start, end ).WithoutTags( "trigger" );
		foreach ( var hit in _recentHits )
			if ( hit.IsValid() ) trace = trace.IgnoreGameObjectHierarchy( hit );
		return trace;
	}

	internal override void HandleContact( SceneTraceResult hit )
	{
		var victim = hit.GameObject?.GetComponentInParent<IDamageable>() as Component;
		if ( victim.IsValid() )
			DamageVictim( (IDamageable)victim, hit.EndPosition, hit.Hitbox );
		if ( !victim.IsValid() || victim.GetComponent<PlayerController>().IsValid() )
		{
			Discharge( hit.EndPosition );
			GameObject.Destroy();
			return;
		}
		_recentHits.Enqueue( victim.GameObject );
		if ( _recentHits.Count > 4 ) _recentHits.Dequeue();
		if ( Target == victim.GameObject ) Target = null;
		WorldPosition = hit.EndPosition + Velocity.Normal;
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void Discharge( Vector3 position )
	{
		if ( !Application.IsDedicatedServer && ModelMount is { } mount )
			NpcAudio.Play( mount.NpcAudio.Find( "weapons/shock_discharge.wav" ), position );
	}
}

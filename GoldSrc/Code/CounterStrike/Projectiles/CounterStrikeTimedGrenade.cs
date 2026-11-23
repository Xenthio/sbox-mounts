using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Hide]
public abstract class CounterStrikeTimedGrenade : TimedGrenade
{
	[Property] public string MountId { get; set; }
	internal GameMount ContentMount => Sandbox.Mounting.Directory.Get( MountId ) as GameMount;
	int _bounces;

	internal override bool DealsImpactDamage => false;

	internal static void ThrowTimed<T>( GameObject owner, Vector3 origin, Vector3 velocity, GameMount mount, string name, string model )
		where T : CounterStrikeTimedGrenade, new()
	{
		if ( mount?.FindFile( $"models/{model}.mdl" ) is not { } path ) return;
		Spawn<T>( owner, origin, velocity, 1.5f, name, $"mount://{mount.Ident}/{path}.vmdl",
			grenade => grenade.MountId = mount.Ident );
	}

	internal sealed override void OnBounce( Rigidbody body )
	{
		if ( _bounces < 5 ) PlayBounceSound();
		if ( _bounces >= 10 )
		{
			body.Velocity = Vector3.Zero;
			body.AngularVelocity = Vector3.Zero;
		}
		_bounces++;
	}

	internal virtual void PlayBounceSound() =>
		ContentMount?.NpcAudio.Emit( "weapons/grenade_hit", WorldPosition, volume: 0.25f );

	internal SceneTrace TraceCover( Vector3 start, Vector3 end ) =>
		Scene.Trace.Ray( start, end ).IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "trigger", "debris", "playercontroller", "player", "npc" );

	internal SceneTraceResult TraceFloor() =>
		TraceCover( WorldPosition + Vector3.Up * 8, WorldPosition + Vector3.Down * 32 ).Run();
}

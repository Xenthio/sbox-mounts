using GoldSrc.CounterStrike;
using GoldSrc.HalfLife;

namespace GoldSrc.ConditionZeroDeletedScenes;

[Hide]
public sealed class CampaignGrenade : TimedGrenade
{
	const string MountId = "czeror";

	[Property] public float BlastDamage { get; set; }

	internal static void Throw( GameObject owner, Vector3 origin, Vector3 velocity, float damage )
	{
		if ( Sandbox.Mounting.Directory.Get( MountId ) is not GameMount mount ||
			mount.FindFile( "models/w_flashbang.mdl" ) is not { } path ) return;

		var name = damage > 0 ? "Deleted Scenes HE grenade" : "Deleted Scenes flashbang";
		Spawn<CampaignGrenade>( owner, origin, velocity, 3.5f, name, $"mount://{MountId}/{path}.vmdl", grenade =>
		{
			grenade.BlastDamage = damage;
			grenade.GameObject.AddComponent<PhysicsFilter>().Body = owner;
		}, mount );
	}

	internal override void OnBounce( Rigidbody body )
	{
		if ( BlastDamage > 0 )
			EffectsMount?.NpcAudio.Emit( "weapons/he_bounce-1.wav", WorldPosition, volume: 0.25f );
		else
			base.OnBounce( body );
	}

	internal override void Detonate()
	{
		var floor = TraceExplosionSurface();
		if ( BlastDamage <= 0 )
		{
			FlashbangProjectile.Flash( GameObject, EffectsMount, floor );
			return;
		}

		var origin = floor.Hit ? floor.EndPosition + floor.Normal * ((BlastDamage - 24) * 0.6f) : WorldPosition;
		Blast.DamageInRadius( GameObject, Thrower, origin, BlastDamage, BlastDamage * 2.5f, "blast" );
		EffectsMount?.NpcAudio.Emit( $"weapons/explode{Game.Random.Int( 3, 5 )}.wav", origin );
		EffectsMount?.NpcAudio.Emit( "weapons/debris", origin, volume: 0.55f );
		ShowExplosion( origin, floor.EndPosition, floor.Normal, floor.Hit );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static async void ShowExplosion( Vector3 origin, Vector3 contact, Vector3 normal, bool hit )
	{
		if ( Application.IsDedicatedServer || Sandbox.Mounting.Directory.Get( MountId ) is not GameMount mount ) return;
		var scene = Game.ActiveScene;
		if ( hit ) ImpactDecal.Place( contact, normal, $"scorch{Game.Random.Int( 1, 2 )}", 48, mount );
		var underwater = WaterBody.Find( scene, origin ).IsValid();
		if ( !underwater )
		{
			Blast.SpriteEffect( "Grenade flash", "zerog-frag1", origin, 2, 30, mount: mount );
			Blast.SpriteEffect( "Grenade dust", "gas_puff_01", origin, 2, 12, additive: false, mount: mount );
		}

		await GameTask.DelaySeconds( 0.55f );
		if ( !scene.IsValid() || scene != Game.ActiveScene ) return;
		if ( underwater ) Blast.Bubbles( origin, 64, 100, "Grenade bubbles", mount );
		else Blast.SmokePuff( "Grenade smoke", origin, 3.5f, mount );
	}
}

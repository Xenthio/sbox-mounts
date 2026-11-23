using GoldSrc.HalfLife;

namespace GoldSrc.CounterStrike;

[Hide]
public sealed class FlashbangProjectile : CounterStrikeTimedGrenade
{
	internal static void Throw( GameObject owner, Vector3 origin, Vector3 velocity, GameMount mount ) =>
		ThrowTimed<FlashbangProjectile>( owner, origin, velocity, mount, "Flashbang", "w_flashbang" );

	internal override void Detonate()
	{
		var floor = TraceFloor();
		var origin = floor.Hit ? floor.EndPosition + floor.Normal * ((35 - 24) * 0.6f) : WorldPosition;
		var underwater = WaterBody.Find( Scene, origin ).IsValid();
		FlashPlayers( origin + Vector3.Up, underwater );
		ContentMount?.NpcAudio.Emit( "weapons/flashbang-", origin, volume: 0.55f );
		ShowEffects( MountId, origin, floor.EndPosition, floor.Normal, floor.Hit, underwater );
	}

	void FlashPlayers( Vector3 origin, bool underwater )
	{
		const float radius = 1500;
		foreach ( var player in Scene.GetAllComponents<PlayerController>() )
		{
			var eye = player.EyePosition;
			var distance = origin.Distance( eye );
			if ( distance >= radius || !Npc.SharesWaterLevel( player.GameObject, underwater ) ) continue;
			var vitals = player.Components.GetAll<Component.IDamageable>().FirstOrDefault( x => x.MaxHealth > 0 );
			if ( vitals is { Health: <= 0 } || Blocked( origin, eye, player ) || Blocked( eye, origin, player ) ) continue;

			var facingFlash = (origin - eye).Dot( player.EyeAngles.ToRotation().Forward ) >= 0;
			var strength = 4 * (1 - distance / radius);
			var fade = strength * (facingFlash ? 3 : 1.75f);
			var hold = strength / (facingFlash ? 1.5f : 3.5f);
			using ( Rpc.FilterInclude( player.Network.Owner ?? Connection.Local ) ) ShowBlindness( player, fade, hold, facingFlash );
		}
	}

	bool Blocked( Vector3 start, Vector3 end, PlayerController target ) =>
		Scene.Trace.Ray( start, end ).IgnoreGameObjectHierarchy( GameObject ).IgnoreGameObjectHierarchy( target.GameObject )
			.WithoutTags( "trigger", "debris" ).Run().Hit;

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void ShowBlindness( PlayerController player, float fade, float hold, bool facingFlash )
	{
		if ( Application.IsDedicatedServer || !player.IsValid() || player.IsProxy ) return;
		player.GameObject.GetOrAddComponent<FlashbangDisplay>().Apply( fade, hold, facingFlash );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static async void ShowEffects( string mountId, Vector3 origin, Vector3 contact, Vector3 normal, bool hit, bool underwater )
	{
		if ( Application.IsDedicatedServer || Sandbox.Mounting.Directory.Get( mountId ) is not GameMount mount ) return;
		var scene = Game.ActiveScene;
		if ( hit ) ImpactDecal.Place( contact, normal, $"scorch{Game.Random.Int( 1, 2 )}", 48, mount );
		if ( !underwater )
		{
			var sparks = Game.Random.Int( 0, 3 );
			for ( var i = 0; i < sparks; i++ ) SparkShower.Spawn( origin, hit ? normal : Vector3.Zero, mount );
		}
		await GameTask.DelaySeconds( 0.3f );
		if ( !scene.IsValid() || scene != Game.ActiveScene ) return;
		if ( underwater ) Blast.Bubbles( origin, 64, 100, "Flashbang bubbles", mount );
		else Blast.SpriteEffect( "Flashbang smoke", "steam1", origin, 2.5f, 6, additive: false, mount: mount );
	}
}

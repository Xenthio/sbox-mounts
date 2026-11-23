using GoldSrc.HalfLife;

namespace GoldSrc.TeamFortressClassic;

[Title( "Fragmentation Grenade" ), Category( "Team Fortress Classic" )]
[WeaponPickup( "grenade", "tfc_frag_grenades", 4, EntityName = "tf_weapon_normalgrenade", Bucket = 4, Mount = "tfc" )]
public sealed class FragGrenade : PinGrenadeWeapon
{
	internal override float MinimumPinTime => 0.8f;
	internal override float HeldFuse => MinimumPinTime + 3;
	internal override string PinSequence => "pinpull";
	internal override string PinSound => "weapons/ax1.wav";
	internal override string WorldModelPath => "models/p_grenade.mdl";
	internal override (string Name, float? Delay) IdleAnimation => ("idle", null);

	internal override void Configure()
	{
		base.Configure();
		StartingAmmo = 4;
	}

	internal override Vector3 LaunchOrigin( Vector3 direction ) =>
		Owner.WorldPosition + Owner.WorldRotation.Up * (Owner.CurrentHeight * 0.5f);

	internal override string LaunchGrenade( Vector3 origin, Vector3 direction, float pitch, float pinAge )
	{
		if ( ContentMount?.FindFile( "models/w_grenade.mdl" ) is not { } model ) return "throw1";
		var fuse = HeldFuse - pinAge;
		var inHand = fuse <= 0;
		var aim = Owner.EyeAngles.ToRotation();
		var velocity = inHand ? Vector3.Zero : aim.Forward * 600
			+ aim.Up * Game.Random.Float( 190, 210 ) + aim.Right * Game.Random.Float( -10, 10 );
		TimedGrenade.Spawn<FortressFragGrenade>( Owner.GameObject, origin, velocity, fuse,
			"TFC fragmentation grenade", $"mount://tfc/{model}.vmdl", grenade =>
			{
				grenade.InHand = inHand;
				if ( Owner.Body.IsValid() ) grenade.AddComponent<PhysicsFilter>().Body = Owner.Body.GameObject;
			} );
		return "throw1";
	}
}

[Hide]
public sealed class FortressFragGrenade : TimedGrenade
{
	const float Damage = 180;
	internal bool InHand { get; set; }
	internal override float GravityScale => 0.81f;
	internal override float Friction => 0.6f;
	static GameMount Mount => Sandbox.Mounting.Directory.Get( "tfc" ) as GameMount;

	internal override void OnBounce( Rigidbody body ) =>
		Mount?.NpcAudio.Emit( "weapons/grenade_hit", WorldPosition, volume: 0.25f );

	internal override void Detonate()
	{
		var position = WorldPosition;
		var floor = Scene.Trace.Ray( position + Vector3.Up * 8, position + Vector3.Down * 40 )
			.IgnoreGameObjectHierarchy( GameObject ).IgnoreGameObjectHierarchy( Thrower ).IgnoreDynamic()
			.WithoutTags( "trigger", "debris" ).Run();
		var damageOrigin = !InHand && floor.Hit ? floor.EndPosition + floor.Normal * 32 : position;
		var effectOrigin = floor.Hit ? floor.EndPosition + floor.Normal * ((Damage - 24) * 0.6f) : position;
		FortressBlast.Apply( GameObject, Thrower, damageOrigin, Damage, falloff: 0.5f, inHand: InHand );
		Mount?.NpcAudio.Emit( $"weapons/explode{Game.Random.Int( 3, 5 )}.wav", effectOrigin );
		Mount?.NpcAudio.Emit( "weapons/debris", effectOrigin, volume: 0.55f, attenuation: 0.8f );
		ShowExplosion( effectOrigin, floor.Normal );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void ShowExplosion( Vector3 position, Vector3 normal )
	{
		if ( Mount is not { } mount ) return;
		Blast.SpriteEffect( "TFC fragmentation grenade explosion", "explode01", position, (Damage - 50) * 0.06f, 15,
			additive: false, mount: mount );
		if ( WaterBody.Find( Game.ActiveScene, position ).IsValid() ) return;
		var count = Game.Random.Int( 0, 3 );
		for ( var i = 0; i < count; i++ )
			SparkShower.Spawn( position, normal, mount );
	}
}

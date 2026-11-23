using GoldSrc.HalfLife;

namespace GoldSrc.CryOfFear;

[Title( "Suicider" ), Category( "Cry of Fear" )]
[NpcDefinition( "monster_suicider", "suicider", MonsterClass.AlienMonster, 40, 16, 72, 203.82f,
	Mount = "cof", Attack = NpcAttack.Bullet, Damage = 5, Range = 1024, Blood = Npc.BloodColor.Red )]
public sealed class Suicider : CryOfFearMeleeNpc
{
	const float CloseRange = 60;
	const string AimController = "suicider_aim";
	static readonly string[] Controls = [null, null, AimController];

	[Property, Hide] public bool UsesP345 { get; set; }
	[Property, Hide] public float DamageScale { get; set; } = 1;
	[Sync( SyncFlags.FromHost )] float AimYaw { get; set; }

	int? _rounds;
	TimeUntil _nextShot;
	bool _finalShot;
	StudioController _aim;

	internal string Report => UsesP345 ? "weapons/p345/p345_fire.wav" : "weapons/glock/glock_fire.wav";
	internal override string[] ControllerParameters => Controls;
	internal override NpcSounds Sounds { get; } = new( Alert: "slower/slower_alert" );

	internal override void ApplyMapProperties( Bsp.Entity entity )
	{
		UsesP345 = entity.GetInt( "iuser1" ) != 0;
		var scale = entity.GetFloat( "fuser1" );
		DamageScale = scale > 0 ? scale : 1;
	}

	internal override void OnModelLoaded( ModelLoader loader )
	{
		EyeHeight = 28;
		_rounds = UsesP345 ? 8 : 15;
		_aim = loader.Controllers is { Length: > 2 } ? loader.Controllers[2] : null;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( _aim is not null && Health > 0 && Renderer.IsValid() )
			Renderer.Animation.Parameters.TrySet( AimController, _aim.Fraction( AimYaw ) );
	}

	internal override void Engage( GameObject target )
	{
		Animation.SetLookTarget( target );
		var offset = target.WorldPosition - WorldPosition;
		AimYaw = (WorldRotation.Inverse * offset).EulerAngles.Normal.yaw;
		var aim = AimAt( target );
		if ( target.GetComponentInParent<PlayerController>().IsValid() && CanHit( target, aim ) )
		{
			var distance = offset.WithZ( 0 ).Length;
			if ( _nextShot <= 0 && (distance <= CloseRange || _rounds == 1) )
			{
				_finalShot = true;
				((IDamageable)this).OnDamage( new DamageInfo( Health, GameObject, GameObject ) { Position = EyePosition } );
				return;
			}
			if ( _nextShot <= 0 && distance <= AttackRange && _rounds > 1 ) Shoot( aim );
		}
		if ( Movement?.MoveTarget is not { } current || current.DistanceSquared( target.WorldPosition ) > 16 * 16 )
			MoveToward( target.WorldPosition, 8 );
	}

	void Shoot( Vector3 aim )
	{
		_rounds--;
		_nextShot = Game.Random.Float( 0.2f, 0.8f );
		var origin = Renderer.GetAttachment( "attachment2" )?.Position ?? EyePosition;
		PlayGunshot( Report, origin, pitch: Game.Random.Int( 95, 105 ) / 100f );
		TraceBullets( origin, aim, 1, (int)(Definition.Damage * DamageScale), 0.06976f, 768, tracerFrequency: 1 );
	}

	internal override void ModifyDamage( Component.IDamageable.DamageEvent damage )
	{
		base.ModifyDamage( damage );
		if ( damage.DamageInfo.Tags.Has( "bullet" ) ) damage.AcceptedDamage *= 0.3f;
	}

	internal override void PainSound()
	{
		if ( Game.Random.Int( 0, 5 ) < 2 )
			EmitSound( "slower/slower_pain", WorldPosition, pitch: Game.Random.Int( 95, 104 ) / 100f );
	}

	internal override StudioSequence ChooseDeathSequence( ITagSet groups, Vector3 direction ) =>
		Find( _finalShot ? "suicide" : Animation.MoveVelocity.Length > 12 ? "deathrun" : "death" )
			?? base.ChooseDeathSequence( groups, direction );

	protected override void OnDeath( Component.IDamageable.DeathEvent death )
	{
		var drop = Renderer.GetAttachment( "attachment2" ) ?? new Transform( EyePosition, WorldRotation );
		if ( UsesP345 ) WeaponPickupLoader.Drop<P345>( drop );
		else WeaponPickupLoader.Drop<Glock>( drop );
		if ( Renderer.Model.Parts.All.Any( part => part.Name == "gun" ) ) Renderer.SetBodyGroup( "gun", 1 );
		base.OnDeath( death );
	}

	internal override NpcCorpse AddCorpseComponent( GameObject go )
	{
		var corpse = go.AddComponent<SuiciderCorpse>();
		corpse.Report = Report;
		go.GetComponent<SkinnedModelRenderer>( true ).CreateAttachments = true;
		return corpse;
	}
}

[Hide]
public sealed class SuiciderCorpse : NpcCorpse
{
	[Property, Hide] public string Report { get; set; }

	internal override bool PlayEventsOnClients => true;

	internal override void HandleEvent( int id )
	{
		if ( id != 1 || DeathSequence != "suicide" || Sandbox.Mounting.Directory.Get( "cof" ) is not GameMount mount ) return;
		var point = Renderer.GetAttachment( "attachment1" )?.Position ?? WorldPosition + Vector3.Up * 56;
		if ( Renderer.Model.Parts.All.FirstOrDefault() is { } body ) Renderer.SetBodyGroup( body.Name, 1 );
		if ( Networking.IsHost )
		{
			mount.NpcAudio.Emit( Report, point, pitch: Game.Random.Int( 95, 105 ) / 100f );
			mount.NpcAudio.Emit( "baby/b_attack", point, pitch: Game.Random.Int( 95, 105 ) / 100f );
			foreach ( var target in Scene.GetAllComponents<IDamageable>().OfType<Component>().ToArray() )
			{
				if ( !target.IsValid() || target.WorldPosition.DistanceSquared( point ) > 64 * 64 ) continue;
				((IDamageable)target).OnDamage( new DamageInfo { Damage = 20, Origin = point, Position = target.WorldPosition, Attacker = GameObject } );
			}
		}
		if ( Application.IsDedicatedServer ) return;
		Blast.SpriteEffect( "Suicider head burst", "head_burst", point, 1, 20, additive: false, mount: mount );
		var model = Model.Load( "mount://cof/cryoffear/models/slower_headgibs.mdl.vmdl" );
		if ( !model.IsValid() || model.IsError || model.Parts.All.FirstOrDefault() is not { } pieces || pieces.Choices.Count == 0 ) return;
		Vector3[] velocities = [new( 50, 0, 50 ), new( -50, 0, 50 ), new( 0, 50, 50 ), new( 0, -50, 50 )];
		for ( var i = 0; i < 5; ++i )
			HalfLife.Gib.Spawn( model, pieces.Name, i % pieces.Choices.Count, point, velocities[System.Math.Min( i, 3 )], Npc.BloodColor.Red, fadeAfter: 2 );
	}
}

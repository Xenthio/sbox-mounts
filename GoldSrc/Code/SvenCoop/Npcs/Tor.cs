using GoldSrc.HalfLife;
using Sandbox.Npcs;

namespace GoldSrc.SvenCoop;

[Title( "Tor" ), Category( "Sven Co-op" )]
[NpcDefinition( "monster_alien_tor", "Tor", MonsterClass.AlienMilitary, 800, 32, 64, 235,
	Mount = "svencoop", ArmorGroup = 10, Attack = NpcAttack.Shock, Damage = 55, Range = 4096,
	Gibs = GibSet.Alien, Blood = BloodColor.Yellow )]
public sealed partial class Tor : VoicedNpc
{
	const float SlamRadius = 300;
	const float MeleeReach = 100;
	const float MeleeChaseRange = 300;
	int _missedMelee;
	int _beamBursts;
	int _beamTicks;
	int _beamAction;
	GameObject _beamTarget;
	TimeUntil _beamCooldown;
	TimeUntil _nextBeamTick;
	TimeUntil _slamCooldown;

	internal override float ViewDot => 0.2f;
	internal override string[] ExtraActions => ["slam", "attack_melee2", "attack_stab", "attack_projec", "summon", "combatidle"];
	internal override NpcSounds Sounds { get; } = new( "tor/tor-idle", "tor/tor-alerted", "tor/tor-pain", "tor/tor-die", "tor/tor-attack" );
	internal override StudioSequence RestSequence => Senses.VisibleHostiles.Any() ? Find( "combatidle" ) ?? base.RestSequence : base.RestSequence;

	internal override void OnModelLoaded( ModelLoader loader ) => EyeHeight = loader.EyePosition.z;

	internal override bool InAttackRange( GameObject target, Vector3 aim )
	{
		var distance = WorldPosition.Distance( target.WorldPosition );
		return distance <= 88 || (distance <= 150 && _slamCooldown <= 0)
			|| (distance < AttackRange && (CanSummon || CanFireBeam( distance )));
	}

	internal override bool Pursue( Vector3 goal ) => MoveToward( goal, 75 );

	bool CanFireBeam( float distance ) => _beamCooldown <= 0 && (distance >= MeleeChaseRange || _missedMelee >= 2);

	internal override void BeginAttack( GameObject target )
	{
		var offset = target.WorldPosition - WorldPosition;
		var distance = offset.Length;
		var facing = WorldRotation.Forward.Dot( offset.WithZ( 0 ).Normal );
		if ( distance <= 150 && _slamCooldown <= 0 && ShouldSlam() && Find( "slam" ) is { } slam )
			PlayAction( slam );
		else if ( distance <= 88 && facing >= 0.6f && WeightedActivity( Melee + 1 ) is { } melee )
		{
			EmitSound( Sounds.Attack, WorldPosition );
			PlayAction( melee );
		}
		else if ( CanSummon && FindSummonPoint() is { } point && Find( "summon" ) is { } summon )
		{
			_summonCandidate = point;
			PlayAction( summon );
		}
		else if ( facing >= 0.5f && CanFireBeam( distance ) && Find( "attack_projec" ) is { } beam )
		{
			_beamTarget = target;
			PlayAction( beam );
			_beamAction = ActionSerial;
		}
		else if ( distance > 88 )
		{
			Pursue( target.WorldPosition );
		}
	}

	internal override void HandleActionEvent( int id )
	{
		switch ( id )
		{
			case 1: Slam(); break;
			case 2: StaffStrike( false ); break;
			case 3: StartBeam(); break;
			case 4: StartSummon(); break;
			case 7: StaffStrike( true ); break;
		}
	}

	internal override void PlaySequenceSound( NpcAudio audio, int id )
	{
		if ( id is 10 or 11 )
			NpcAudio.Play( audio.Find( "tor/tor-foot.wav" ), WorldPosition, pitch: Game.Random.Float( 0.9f, 1.1f ) );
	}

	internal override void ModifyDamage( Component.IDamageable.DamageEvent hit )
	{
		if ( ShellHit( hit.DamageInfo ) ) AbsorbShellHit( hit );
		else base.ModifyDamage( hit );
	}

	internal override void ShowWound( Component.IDamageable.DamageEvent hit )
	{
		if ( !ShellHit( hit.DamageInfo ) ) base.ShowWound( hit );
	}

	void StaffStrike( bool stab )
	{
		var victim = TraceClaw( MeleeReach, Definition.Damage, "slash" );
		if ( !victim.IsValid() )
		{
			_missedMelee++;
			return;
		}

		_missedMelee = 0;
		var push = stab ? WorldRotation.Forward * 100 : WorldRotation.Forward * 200 + WorldRotation.Right * 200 + Vector3.Up * 200;
		if ( victim.GameObject.GetComponent<PlayerController>() is { } player )
			PunchPlayer( player, push, stab ? new Angles( 18, 0, 0 ) : new Angles( 5, 0, 18 ) );
		else
			PushVictim( victim, push );
	}

	void StartBeam()
	{
		if ( _beamAction != ActionSerial ) _beamTarget = FindVisibleEnemy();
		_beamAction = ActionSerial;
		_beamTicks = 3;
		_nextBeamTick = 0;
		_missedMelee = 0;
		if ( ++_beamBursts < 5 ) return;
		_beamCooldown = 3;
		_beamBursts = 0;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( !Networking.IsHost || Health <= 0 ) return;

		UpdateSummon();
		if ( Action is null || _beamAction != ActionSerial || !IsLiveTarget( _beamTarget ) )
		{
			_beamTicks = 0;
			return;
		}

		while ( _beamTicks > 0 && _nextBeamTick <= 0 )
		{
			_nextBeamTick = 0.05f + (float)_nextBeamTick;
			_beamTicks--;
			FireBeamTick();
		}
	}

	void FireBeamTick()
	{
		var start = Renderer.GetAttachment( "attachment1" )?.Position ?? EyePosition;
		var end = start + (TargetBodyPosition( _beamTarget ) - start).Normal * AttackRange;
		var hit = Scene.Trace.Ray( start, end ).IgnoreGameObjectHierarchy( GameObject ).WithoutTags( "trigger", "playercontroller" ).UseHitboxes().Run();
		EmitSound( "tor/tor-staff-discharge.wav", WorldPosition );
		ShowBeam( start, hit.EndPosition, false );
		if ( hit.GameObject?.GetComponentInParent<IDamageable>() is not Component victim ) return;
		ApplyAttackDamage( victim, start, hit.EndPosition, 3, "shock" );
		var grounded = victim.GameObject.GetComponent<PlayerController>()?.IsOnGround ?? (victim as BaseNpc)?.IsOnGround ?? false;
		PushVictim( victim, Vector3.Up * (grounded ? 200 : 120) );
	}

	IEnumerable<Component> NearbyEnemies( float radius ) => Scene.FindInPhysics( new Sphere( WorldPosition, radius ) )
		.Select( go => go.GetComponentInParent<IDamageable>() ).OfType<Component>().Distinct()
		.Where( victim => victim.IsValid() && victim.GameObject != GameObject
			&& (victim is BaseNpc || victim.GameObject.GetComponent<PlayerController>().IsValid())
			&& WorldPosition.DistanceSquared( victim.WorldPosition ) <= radius * radius
			&& IsLiveTarget( victim.GameObject ) && GetDisposition( victim.GameObject ) == Disposition.Hostile );

	bool ShouldSlam()
	{
		var nearby = NearbyEnemies( 150 ).ToArray();
		return nearby.Length >= 2 || nearby.Any( victim => victim.GameObject.GetComponent<PlayerController>().IsValid() );
	}

	void Slam()
	{
		_slamCooldown = Game.Random.Float( 3.5f, 5.5f );
		foreach ( var victim in NearbyEnemies( SlamRadius ).ToArray() )
		{
			var delta = victim.WorldPosition - WorldPosition;
			var lift = System.MathF.Max( 0.7f, 1 - (delta.Length - 64) * 0.5f / SlamRadius );
			var push = Vector3.Up * 500 * lift + delta.Normal.WithZ( 0 ) * 700 * (1 - lift);
			ApplyAttackDamage( victim, WorldPosition, TargetBodyPosition( victim.GameObject ), 15 * lift, "sonic" );
			if ( victim.GameObject.GetComponent<PlayerController>() is { } player )
				PunchPlayer( player, push, new Angles( 10, 0, 0 ) );
			else
				PushVictim( victim, push );
		}

		EmitSound( "houndeye/he_blast", WorldPosition, pitch: Game.Random.Float( 0.95f, 1.05f ) );
		ShowSlam( WorldPosition );
	}

	void ApplyAttackDamage( Component victim, Vector3 origin, Vector3 point, float amount, string tag )
	{
		var damage = new DamageInfo { Damage = amount, Origin = origin, Position = point, Attacker = GameObject, Weapon = GameObject };
		damage.Tags.Add( tag );
		((IDamageable)victim).OnDamage( damage );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void ShowSlam( Vector3 origin ) => SonicRing.Spawn( origin, Color.White, 350, SvenMount );

	static GameMount SvenMount => Sandbox.Mounting.Directory.Get( "svencoop" ) as GameMount;

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void ShowBeam( Vector3 start, Vector3 end, bool summoning )
	{
		if ( summoning )
			EnergyBeam.Show( start, end, new Color32( 96, 255, 32, 80 ), "xenobeam", "Tor summoning beam", 3, 2, SvenMount );
		else
		{
			EnergyBeam.Show( start, end, new Color32( 96, 128, 16, 150 ), "xenobeam", "Tor staff beam", 5, 0.5f, SvenMount );
			EnergyBeam.Show( start, end, new Color32( 96, 255, 16, 150 ), "xenobeam", "Tor staff glow", 3, 0.5f, SvenMount );
		}
	}
}

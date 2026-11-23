namespace GoldSrc.HalfLife;

[Title( "Alien Grunt" ), Category( "Half-Life" )]
[Alias( "GoldSrc.HalfLifeAlienGrunt" )]
[NpcDefinition( "monster_alien_grunt", "agrunt", MonsterClass.AlienMilitary, 90, 32, 64, 125, ArmorGroup = 10, Attack = NpcAttack.Projectile, Damage = 5, Range = 1024, ProjectileModel = "hornet", Gibs = GibSet.Alien, Blood = BloodColor.Yellow )]
public partial class AlienGrunt : VoicedNpc, Component.IDamageable.IEvents
{
	internal override float ViewDot => 0.2f;

	protected override Sandbox.Npcs.Relationship GetFallbackRelationship( GameObject other ) =>
		other.GetComponent<Npc>() is HumanGrunt ? new( Sandbox.Npcs.Disposition.Hostile, 2 ) : base.GetFallbackRelationship( other );

	internal override NpcSounds Sounds { get; } = new( "agrunt/ag_idle", "agrunt/ag_alert", "agrunt/ag_pain", "agrunt/ag_die", "agrunt/ag_fire" );

	internal override IEnumerable<StudioSequence> ExtraSequences => [.. Activities( Melee ), .. Activities( Ranged ), .. VictoryActivities.Distinct().SelectMany( Activities )];
	GameObject _hornetTarget;
	int _volley = -1;

	internal override void OnModelLoaded( ModelLoader loader )
	{
		EyeHeight = loader.EyePosition.z;
		PauseSpeech();
	}

	internal override bool InAttackRange( GameObject target, Vector3 aim ) =>
		WorldPosition.DistanceSquared( target.WorldPosition ) <= AttackRange * AttackRange;

	internal override void BeginAttack( GameObject target )
	{
		var offset = target.WorldPosition - WorldPosition;
		var dot = WorldRotation.Forward.Dot( offset.WithZ( 0 ).Normal );
		if ( offset.LengthSquared <= 100 * 100 && dot >= 0.6f && WeightedActivity( Melee ) is { } punch )
		{
			CombatVoice( "agrunt/ag_attack" );
			PlayAction( punch );
			return;
		}
		if ( offset.LengthSquared < 100 * 100 || dot < 0.5f || WeightedActivity( Ranged ) is not { } volley ) return;
		PlayAction( volley );
		_volley = ActionSerial;
		_hornetTarget = target;
	}

	internal override void PlaySequenceSound( NpcAudio audio, int id )
	{
		if ( id is not (10 or 11) ) return;
		var step = (id == 10 ? 2 : 1) + 2 * Game.Random.Int( 0, 1 );
		NpcAudio.Play( audio.Find( $"player/pl_ladder{step}.wav" ), WorldPosition, pitch: 0.7f );
	}

	internal override void HandleActionEvent( int id )
	{
		if ( id is >= 1 and <= 5 )
		{
			var target = _volley == ActionSerial ? _hornetTarget : FindVisibleEnemy();
			var direction = IsLiveTarget( target ) && Senses.VisibleHostiles.Contains( target )
				? (target.WorldPosition - WorldPosition).Normal : WorldRotation.Forward;
			SetAimBlend( Find( Action ), direction );
			var muzzle = AttackOrigin + direction * 32;
			MuzzleFlash( muzzle, "muz4", 24 );
			NpcProjectile.Fire<Hornet>( GameObject, ProjectilePath, Definition.Damage, target, muzzle, direction, null );
			PlayAttackSound( WorldPosition );
			return;
		}
		if ( id is not (12 or 13) ) return;
		var victim = TraceClaw( 100, 20, "club", Game.Random.Int( 95, 105 ) / 100f );
		if ( !victim.IsValid() ) return;
		var player = victim.GameObject.GetComponent<PlayerController>();
		if ( player.IsValid() )
			PunchPlayer( player, WorldRotation.Right * (id == 12 ? 250 : -250), new Angles( 8, id == 12 ? -25 : 25, 0 ) );

		var blood = victim is Npc npc ? npc.Definition.Blood : player.IsValid() || victim is Sandbox.Npcs.BaseNpc ? BloodColor.Red : BloodColor.None;
		if ( blood != BloodColor.None ) BloodEffects.Spray( blood, AttackOrigin, 25 );
	}

	internal override void ModifyDamage( Component.IDamageable.DamageEvent hit )
	{
		if ( ShellHit( hit.DamageInfo ) ) AbsorbShellHit( hit );
	}

	internal override void ShowWound( Component.IDamageable.DamageEvent hit )
	{
		if ( !ShellHit( hit.DamageInfo ) ) base.ShowWound( hit );
	}
}

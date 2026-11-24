namespace GoldSrc.HalfLife;

[Hide]
public abstract partial class AutomatedTurret : Npc
{
	internal override float ViewDot => -1;
	internal override float SightRange => 1200;

	public enum Mode { Retracted, Deploying, Searching, Firing, Retiring }

	[Sync( SyncFlags.FromHost ), Change( nameof( StateChanged ) )] Mode State { get; set; }
	[Sync( SyncFlags.FromHost )] Angles Aim { get; set; }
	[Sync( SyncFlags.FromHost )] float SpinRate { get; set; }
	[Sync( SyncFlags.FromHost )] byte EyeBrightness { get; set; }
	internal override float MovementPlaybackRate( StudioSequence sequence, float speed ) =>
		sequence.Name.Equals( "spin", System.StringComparison.OrdinalIgnoreCase ) ? SpinRate : base.MovementPlaybackRate( sequence, speed );
	internal virtual bool HasRotaryBarrel => false;
	internal virtual float SearchDuration => 15;
	internal virtual bool AutoDeploy => true;
	internal virtual float RetractedHeight => 16;
	internal virtual float DeployedHeight => 32;
	internal virtual float GunHeight => 12.75f;
	internal virtual float RetractedDamageScale => 0.1f;
	internal virtual bool CanBerserk => true;
	internal virtual float MinimumElevation => -15;
	internal virtual float ShotAttenuation => GoldSrcSoundEvents.Normal;
	internal virtual float ShotDamage => Definition.Damage;
	internal virtual float ShotSpread => 0;
	internal virtual string ShotSound => $"weapons/hks{Game.Random.Int( 1, 3 )}.wav";
	internal override NpcCorpse AddCorpseComponent( GameObject go )
	{
		var corpse = go.AddComponent<TurretWreck>();
		corpse.Radius = Definition.Radius;
		corpse.Height = Definition.Height;
		return corpse;
	}
	internal override NpcSounds Sounds { get; } = new( Death: "turret/tu_die" );
	static readonly string[] Clips = ["deploy", "retire", "spin", "fire"];
	internal override string[] ExtraActions => Clips;
	internal override StudioSequence RestSequence => State == Mode.Firing ? Find( "fire" ) : State is Mode.Searching or Mode.Retiring ? Find( "spin" ) : base.RestSequence;

	BoxCollider _hull;
	bool _berserk;
	Angles _erraticAim;
	TimeUntil _nextThink, _ready, _nextPing;
	TimeSince _sinceEnemy;
	float _scanYaw, _turnRate = 30;
	GameObject _enemy;
	Vector3 _lastSight;
	bool _loaded;
	bool? _powerRequest;
	protected bool IsDeployed => State != Mode.Retracted;
	internal override void OnModelLoaded( ModelLoader loader )
	{
		_gun.Bind( loader, Renderer );
		Animation.TurnsBody = false;
		EyeHeight = GunHeight;
		_hull = GetComponentInChildren<BoxCollider>();
		StateChanged( State, State );
		_loaded = true;
	}

	void StateChanged( Mode before, Mode after )
	{
		if ( !_hull.IsValid() ) return;
		_hull.Center = Vector3.Zero;
		var halfHeight = after is Mode.Retracted or Mode.Deploying ? RetractedHeight : DeployedHeight;
		_hull.Scale = new Vector3( Definition.Radius * 2, Definition.Radius * 2, halfHeight * 2 );
	}

	bool FindVisiblePoint( GameObject enemy, Vector3 eye, out Vector3 point )
	{
		point = default;
		if ( !SharesWaterLevel( enemy, WaterBody.Find( Scene, eye ).IsValid() ) ) return false;

		var bounds = TargetHullBounds( enemy );
		for ( int i = 0; i < 5; i++ )
		{
			point = bounds.RandomPointInside;
			var sight = Scene.Trace.Ray( eye, point ).IgnoreGameObjectHierarchy( GameObject )
				.IgnoreGameObjectHierarchy( enemy ).WithoutTags( "npc", "player", "playercontroller", "trigger", "glass" ).Run();
			if ( !sight.Hit && !sight.StartedSolid ) return eye.Distance( point ) <= Definition.Range;
		}
		return false;
	}

	protected override void Think()
	{
		if ( !_loaded ) return;
		if ( _powerRequest is { } enabled && State != Mode.Retiring )
		{
			_powerRequest = null;
			if ( enabled ) Deploy();
			else if ( State != Mode.Retracted )
			{
				InterruptAction();
				_enemy = null;
				State = Mode.Retiring;
			}
		}
		if ( AdvanceAction() || _nextThink > 0 || State == Mode.Retracted && !AutoDeploy ) return;
		_nextThink = State == Mode.Retracted ? 0.3f : 0.1f;
		if ( State == Mode.Retiring )
		{
			AimToRest();
			return;
		}

		var enemy = IsLiveTarget( _enemy ) ? _enemy : FindVisibleEnemy();
		var eye = WorldPosition + WorldRotation.Up * EyeHeight;
		var origin = State == Mode.Retracted ? eye : Renderer.GetAttachment( "attachment1" )?.Position ?? eye;
		var end = eye;
		var visible = enemy.IsValid() && FindVisiblePoint( enemy, eye, out end );
		if ( visible )
		{
			_enemy = enemy;
			_lastSight = end;
			_sinceEnemy = 0;
		}
		if ( visible || _enemy is not null && _sinceEnemy < 0.5f )
		{
			var direction = _lastSight - origin;
			if ( State == Mode.Retracted )
			{
				EmitSound( "turret/tu_alert.wav", WorldPosition, 0.5f );
				Deploy();
				return;
			}
			var desired = (WorldRotation.Inverse * direction).EulerAngles;
			desired.pitch = desired.Normal.pitch.Clamp( -90, -MinimumElevation );
			if ( _berserk && Game.Random.Int( 0, 9 ) == 0 )
			{
				_erraticAim = new Angles( Game.Random.Float( -90, 0 ), Game.Random.Float( -180, 180 ), 0 );
				((Component.IDamageable)this).OnDamage( new DamageInfo { Damage = 1, Attacker = GameObject } );
				if ( Health <= 0 ) return;
			}
			if ( visible || _berserk ) TurnTo( _berserk ? _erraticAim : desired );
			_scanYaw = Aim.yaw;
			if ( !SpinUp( origin ) || !_berserk && Vector3.Dot( WorldRotation * Aim.Forward, direction.Normal ) < 0.866f )
			{
				State = Mode.Searching;
				return;
			}
			State = Mode.Firing;
			TraceBullets( origin, origin + WorldRotation * Aim.Forward * Definition.Range, 1, ShotDamage, ShotSpread, Definition.Range, tracerFrequency: 1 );
			PlayGunshot( ShotSound, origin, attenuation: ShotAttenuation );
			return;
		}

		if ( State == Mode.Retracted ) return;
		State = Mode.Searching;
		if ( _enemy is not null )
		{
			_enemy = null;
			_sinceEnemy = 0;
			_nextPing = 1;
		}
		if ( HasRotaryBarrel && _sinceEnemy > 5 ) SpinDown( origin );
		if ( _sinceEnemy >= SearchDuration )
		{
			AimToRest();
			return;
		}
		_scanYaw = Angles.NormalizeAngle( _scanYaw + 3 );
		TurnTo( new Angles( 0, _scanYaw, 0 ) );
		if ( _nextPing <= 0 )
		{
			EmitSound( "turret/tu_ping.wav", origin );
			EyeBrightness = 255;
			_nextPing = 1;
		}
		else EyeBrightness = (byte)System.Math.Max( EyeBrightness - 30, 0 );
	}

	protected void Deploy()
	{
		if ( !_loaded || State != Mode.Retracted ) return;
		State = Mode.Deploying;
		_sinceEnemy = 0;
		EmitSound( "turret/tu_deploy.wav", WorldPosition, 0.5f );
		PlayAction( Find( "deploy" ) );
	}

	protected bool SetPower( bool? enabled )
	{
		var active = _powerRequest ?? (State is not (Mode.Retracted or Mode.Retiring));
		_powerRequest = enabled ?? !active;
		return _powerRequest.Value;
	}

	void AimToRest()
	{
		TurnTo( Angles.Zero );
		if ( System.MathF.Abs( Aim.yaw ) + System.MathF.Abs( Aim.pitch ) >= 1 ) return;
		State = Mode.Retiring;
		EmitSound( "turret/tu_deploy.wav", WorldPosition, 0.5f, 1.2f );
		PlaySequence( Find( "retire" ), true );
	}

	void TurnTo( Angles desired )
	{
		var step = (desired - Aim).Normal;
		var pitchStep = _turnRate * 0.1f;
		var yaw = System.MathF.Abs( step.yaw );
		if ( yaw > 30 ) _turnRate = System.MathF.Min( 300, _turnRate + 30 );
		else if ( yaw > 0 ) _turnRate += _turnRate > 45 ? -30 : 30;
		else if ( step.pitch == 0 ) _turnRate = 30;
		var yawStep = _turnRate * 0.1f;
		Aim = (Aim + new Angles( step.pitch.Clamp( -pitchStep, pitchStep ), step.yaw.Clamp( -yawStep, yawStep ), 0 )).Normal;
	}

	internal override void FinishAction( string name )
	{
		if ( State == Mode.Deploying )
		{
			State = Mode.Searching;
			_sinceEnemy = 0;
			_nextPing = 1;
		}
		else if ( State == Mode.Retiring )
		{
			State = Mode.Retracted;
			SpinRate = 0;
			EyeBrightness = 0;
		}
	}

	internal override void ModifyDamage( Component.IDamageable.DamageEvent damage )
	{
		var info = damage.DamageInfo;
		if ( HitGroups( info ).Has( "hitgroup_10" ) )
		{
			if ( ShouldShowArmorImpact() ) ArmorRicochet.Show( info.Position, Game.Random.Float( 1, 2 ) );
			damage.AcceptedDamage = 0.1f;
		}
		if ( State == Mode.Retracted ) damage.AcceptedDamage *= RetractedDamageScale;
		else if ( CanBerserk && Health - damage.AcceptedDamage <= 10 ) _berserk = true;
	}

}

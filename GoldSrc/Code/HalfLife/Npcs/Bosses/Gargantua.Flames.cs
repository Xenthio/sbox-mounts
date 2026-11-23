namespace GoldSrc.HalfLife;

public abstract partial class GargantuaNpc
{
	static readonly string[] Controls = ["flame_yaw", "flame_pitch"];
	internal override string[] ControllerParameters => Controls;
	StudioController[] _controllers;
	[Sync( SyncFlags.FromHost )] Angles FlameAim { get; set; }
	internal override void OnModelLoaded( ModelLoader loader ) => _controllers = loader.Controllers;

	[Sync( SyncFlags.FromHost )] bool Flaming { get; set; }
	[Sync( SyncFlags.FromHost )] Vector3 LeftEnd { get; set; }
	[Sync( SyncFlags.FromHost )] Vector3 RightEnd { get; set; }
	TimeUntil _nextFlame, _endFlame, _nextBurn;
	ParticleEffect _impactSparks;
	readonly List<BeamEffect> _flames = [];
	SoundHandle _flameSound;

	internal override StudioSequence RestSequence => Flaming ? Find( "shootflames2" ) : base.RestSequence;

	void StartFlames()
	{
		FlameAim = default;
		Flaming = true;
		_endFlame = 4.5f;
		_nextFlame = 6;
		_nextBurn = 0;
		EmitSound( VoiceSample( "gar_flameon1.wav" ), WorldPosition );
		Burn();
	}

	protected override void Think()
	{
		EnemyVisible = Senses?.VisibleHostiles.Count > 0;
		if ( !EnemyVisible ) _nextStomp = 5;
		if ( !Flaming )
		{
			base.Think();
			return;
		}
		if ( _endFlame <= 0 || !_enemy.IsValid() )
		{
			Flaming = false;
			FlameAim = default;
			EmitSound( VoiceSample( "gar_flameoff1.wav" ), WorldPosition );
			return;
		}
		if ( _nextBurn <= 0 ) Burn();
	}

	void Burn()
	{
		_nextBurn = 0.1f;
		var direction = WorldRotation * FlameAim.Forward;
		LeftEnd = BurnFrom( "attachment2", direction );
		RightEnd = BurnFrom( "attachment3", direction );

		var offset = WorldRotation.Inverse * (TargetBodyPosition( _enemy ) - (WorldPosition + WorldRotation.Up * (64 * AttackScale)));
		var desired = offset.EulerAngles.Normal;
		if ( offset.Length > 400 * AttackScale || System.MathF.Abs( desired.yaw ) > 60 )
		{
			_endFlame -= 0.5f;
			_nextFlame -= 0.5f;
		}
		FlameAim = new Angles( FlameAim.pitch.Approach( desired.pitch, 4 ), FlameAim.yaw.Approach( desired.yaw.Clamp( -45, 45 ), 8 ), 0 );
	}

	Vector3 BurnFrom( string hand, Vector3 aim )
	{
		var origin = Renderer.GetAttachment( hand )?.Position ?? WorldPosition + Vector3.Up * (64 * AttackScale);
		var trace = Scene.Trace.Ray( origin, origin + aim * (330 * AttackScale) ).IgnoreGameObjectHierarchy( GameObject ).UseHitboxes().Run();
		if ( trace.Hit )
		{
			var decal = trace.GameObject?.GetComponentInParent<IDamageable>() is null ? Game.Random.Int( 1, 3 ) : 0;
			FlameImpact( trace.EndPosition, trace.Normal, decal );
		}

		var length = trace.EndPosition.Distance( origin );
		var center = (origin + trace.EndPosition) * 0.5f;
		var victims = Scene.FindInPhysics( new Sphere( center, length * 0.5f ) )
			.Select( obj => obj.GetComponentInParent<IDamageable>() ).OfType<Component>().Distinct().ToArray();
		foreach ( var victim in victims )
		{
			if ( !victim.IsValid() || victim.GameObject == GameObject || ClassOf( victim.GameObject ) == MonsterClass.AlienMonster ) continue;
			var target = TargetBodyPosition( victim.GameObject );
			var near = origin + aim * System.Math.Clamp( Vector3.Dot( target - origin, aim ), 0, length );
			var obstruction = Scene.Trace.Ray( near, target ).IgnoreGameObjectHierarchy( GameObject ).UseHitboxes().Run();
			if ( obstruction.Hit && obstruction.GameObject?.GetComponentInParent<IDamageable>() != (IDamageable)victim ) continue;
			var damage = FlameDamage - System.MathF.Max( near.Distance( obstruction.EndPosition ) - 64 * AttackScale, 0 ) * 0.4f;
			if ( damage <= 0 ) continue;
			var info = new DamageInfo { Damage = damage, Origin = near, Position = obstruction.EndPosition, Hitbox = obstruction.Hitbox, Attacker = GameObject, Weapon = GameObject };
			info.Tags.Add( "burn" );
			((IDamageable)victim).OnDamage( info );
		}
		return trace.EndPosition;
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void FlameImpact( Vector3 position, Vector3 normal, int decal )
	{
		if ( Application.IsDedicatedServer || EffectsMount is not { } mount ) return;
		if ( !_impactSparks.IsValid() )
		{
			var go = new GameObject( GameObject, false, "Gargantua flame impacts" ) { NetworkMode = NetworkMode.Never };
			_impactSparks = StreakSplash.Create( go, mount );
			go.Enabled = true;
		}
		StreakSplash.Emit( _impactSparks, position, normal * 50, new Color32( 255, 130, 90 ), 20 );
		if ( decal > 0 ) ImpactDecal.Place( position, normal, $"smscorch{decal}", 24 );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		UpdateEye();
		if ( _controllers is not null && Renderer.IsValid() && Renderer.Animation.IsValid )
		{
			if ( _controllers[0] is { } yaw ) Renderer.Animation.Parameters.TrySet( Controls[0], yaw.Fraction( FlameAim.yaw ) );
			if ( _controllers[1] is { } pitch ) Renderer.Animation.Parameters.TrySet( Controls[1], pitch.Fraction( FlameAim.pitch ) );
		}
		if ( !Flaming )
		{
			if ( _flames.Count > 0 ) StopFlameEffects();
			return;
		}
		if ( _flames.Count == 0 ) CreateFlameEffects();
		for ( int i = 0; i < _flames.Count; ++i )
		{
			var effect = _flames[i];
			var end = i % 2 == 0 ? LeftEnd : RightEnd;
			effect.TargetPosition = i < 2 ? end : Vector3.Lerp( effect.WorldPosition, end, 0.4f );
		}
		if ( _flameSound.IsValid() ) _flameSound.Position = WorldPosition;
	}

	void CreateFlameEffects()
	{
		if ( EffectsMount is not { } mount ) return;
		var sprite = mount.GetIndexedSprite( "xbeam3" );
		for ( int i = 0; i < 4; ++i )
		{
			var hand = Renderer.GetAttachmentObject( i % 2 == 0 ? "attachment2" : "attachment3" );
			var go = new GameObject( hand.IsValid() ? hand : GameObject, false, "Gargantua flame" );
			go.NetworkMode = NetworkMode.Never;
			go.LocalPosition = Vector3.Zero;
			var effect = go.AddComponent<BeamEffect>();
			effect.BeamLifetime = 5;
			effect.Scale = (i < 2 ? 24 : 14) * AttackScale;
			Color color = i < 2 ? new Color32( 255, 130, 90 ) : new Color32( 0, 120, 255 );
			effect.BeamColor = color;
			effect.Brightness = 0.75f;
			effect.TextureScrollSpeed = 20;
			effect.Additive = true;
			if ( sprite is not null ) effect.Texture = sprite.Animations[0].Frames[0].Texture;
			if ( i < 2 )
			{
				var light = go.AddComponent<PointLight>();
				light.Radius = Game.Random.Float( 32, 48 );
				light.LightColor = Color.White;
				light.Shadows = false;
			}
			go.Enabled = true;
			_flames.Add( effect );
		}
		if ( mount.NpcAudio.Find( VoiceSample( "gar_flamerun1.wav" ) ) is { } path && SoundFile.Load( path ) is { } sound )
			_flameSound = GoldSrcSoundEvents.Play( sound, WorldPosition );
	}

	void StopFlameEffects()
	{
		foreach ( var flame in _flames )
			if ( flame.IsValid() ) flame.GameObject.Destroy();
		_flames.Clear();
		_flameSound?.Stop();
	}

	protected override void OnDisabled()
	{
		StopFlameEffects();
		base.OnDisabled();
	}
}

using System;

namespace GoldSrc.HalfLife;

public partial class Npc
{
	float _lastArmorImpact = float.NegativeInfinity;

	internal bool ShouldShowArmorImpact( int chances = 1, int outcomes = 11 )
	{
		if ( _lastArmorImpact == Time.Now && Game.Random.Int( 0, outcomes - 1 ) >= chances ) return false;
		_lastArmorImpact = Time.Now;
		return true;
	}

	internal static bool ShellHit( DamageInfo info ) => HitGroups( info ).Has( "hitgroup_10" )
		&& (info.Tags.Has( "bullet" ) || info.Tags.Has( "club" ) || info.Tags.Has( "slash" ));

	internal void AbsorbShellHit( Component.IDamageable.DamageEvent hit )
	{
		var info = hit.DamageInfo;
		if ( ShouldShowArmorImpact() ) ArmorRicochet.Show( info.Position, Game.Random.Float( 1, 2 ) );
		if ( Game.Random.Int( 0, 1 ) == 0 )
		{
			var reflection = (info.Origin - info.Position).Normal + Vector3.Random * 0.3f;
			BulletEffects.Tracer( info.Position, info.Position + reflection * 512 );
		}
		hit.AcceptedDamage -= 20;
		if ( hit.AcceptedDamage <= 0 ) hit.AcceptedDamage = 0.1f;
	}

	internal virtual string HelmetHitGroup => null;

	internal bool IsHeadHit( ITagSet groups ) => groups.Has( "hitgroup_1" ) || (HelmetHitGroup is { } helmet && groups.Has( helmet ));

	internal virtual void ModifyDamage( Component.IDamageable.DamageEvent hit )
	{
		if ( IsHeadHit( HitGroups( hit.DamageInfo ) ) ) hit.AcceptedDamage *= 3;
	}

	internal static void AbsorbHelmetHit( Component.IDamageable.DamageEvent hit )
	{
		hit.AcceptedDamage -= 20;
		if ( hit.AcceptedDamage > 0 ) return;
		ArmorRicochet.Show( hit.DamageInfo.Position, 1 );
		hit.AcceptedDamage = 0.01f;
	}

	internal bool IsAttacking( GameObject target ) => _action is not null && _hitTimes.Length > 0 && _target == target;
	protected bool IsFirstStrike => _nextHit <= 1;

	internal virtual float AttackRange => Definition.Range;
	internal virtual float StrikeRange => Definition.Range;

	internal virtual bool InAttackRange( GameObject target, Vector3 aim ) => AttackOrigin.Distance( aim ) <= AttackRange;

	internal virtual void Engage( GameObject target )
	{
		Animation.SetLookTarget( target );
		var targetPoint = AimAt( target );
		var inRange = InAttackRange( target, targetPoint );
		if ( inRange && CanHit( target, targetPoint ) )
		{
			Movement?.Stop();
			if ( _nextAttack <= 0 )
				BeginAttack( target );
			return;
		}

		if ( inRange && TryOccludedAttack( target ) ) return;

		if ( _nextMove > 0 )
			return;
		_nextMove = 0.18f;
		var goal = Definition.Movement == NpcMovement.Ground ? target.WorldPosition : targetPoint;
		var failed = Movement?.Status == Sandbox.Npcs.Layers.MovementLayer.MovementStatus.Failed;
		if ( !Pursue( goal ) || failed )
			OnPursuitFailed( target );
	}

	internal virtual bool Pursue( Vector3 goal ) => MoveToward( goal, Math.Clamp( AttackRange * 0.5f, 9, 130 ) );

	internal virtual void OnPursuitFailed( GameObject target ) { }

	internal virtual bool TryOccludedAttack( GameObject target ) => false;

	internal virtual void BeginAttack( GameObject target ) => AttackWith( target, _attack );

	protected void FaceCorpse( Vector3 corpse )
	{
		Movement?.Stop();
		var facing = (corpse - WorldPosition).WithZ( 0 );
		if ( facing.LengthSquared > 0 ) WorldRotation = Rotation.LookAt( facing );
	}

	internal void AttackWith( GameObject target, StudioSequence attack, float? playbackRate = null )
	{
		_target = target;
		var duration = attack?.Duration ?? 0.65f;
		var rate = playbackRate ?? PlaybackRate;
		_nextAttack = duration / rate + (Definition.Attack == NpcAttack.Bullet && attack?.PitchFrames is not null ? 0 : 0.3f);
		if ( attack is null )
		{
			_nextHit = 0;
			Strike();
			return;
		}

		SetAimBlend( attack, AimAt( target ) - AimOrigin );
		PlaySequence( attack, false, rate );
		_hitTimes = attack.Events.Where( x => Definition.StrikeEvents?.Contains( x.Index ) == true )
			.Select( x => Math.Clamp( x.Frame / attack.Description.Fps, 0, duration ) )
			.Distinct().Order().ToArray();
		if ( _hitTimes.Length == 0 )
			_hitTimes = [duration * 0.4f];
	}

	internal void PlayAction( StudioSequence sequence ) => PlaySequence( sequence, false );

	internal void PlaySequence( StudioSequence sequence, bool reverse, float? playbackRate = null )
	{
		ActionReversed = reverse;
		_hitTimes = [];
		_nextEvent = 0;
		_nextHit = 0;
		_action = sequence;
		Action = sequence.Name;
		ActionRate = playbackRate ?? PlaybackRate;
		ActionStart = 0;
		ActionSerial++;
	}

	internal void InterruptAction()
	{
		_action = null;
		Action = null;
		_hitTimes = [];
	}

	internal bool AdvanceAction()
	{
		if ( _action is null )
		{
			// A new host has no local hit schedule for the previous host's action.
			if ( Action is not null ) Action = null;
			return false;
		}
		while ( _nextEvent < _action.Events.Length )
		{
			var e = _action.Events[ActionReversed ? _action.Events.Length - 1 - _nextEvent : _nextEvent];
			var eventTime = e.Frame / _action.Description.Fps;
			if ( ActionTime < (ActionReversed ? _action.Duration - eventTime : eventTime) ) break;
			_nextEvent++;
			HandleActionEvent( e );
		}
		while ( _nextHit < _hitTimes.Length && ActionTime >= _hitTimes[_nextHit] )
		{
			_nextHit++;
			Strike();
		}
		if ( ActionTime < _action.Duration ) return true;
		var finished = _action.Name;
		_action = null;
		Action = null;
		_hitTimes = [];
		FinishAction( finished );
		return _action is not null;
	}

	internal virtual Vector3 AimOrigin => AttackOrigin;

	internal virtual Vector3 AttackOrigin => (Definition.Attack is NpcAttack.Bullet or NpcAttack.Projectile
		? Renderer.GetAttachment( "attachment1" )?.Position : null) ?? WorldPosition + Vector3.Up * (EyeHeight * 0.8f);
	internal static Vector3 TargetEyePosition( GameObject target )
	{
		if ( target.GetComponent<PlayerController>() is { } player )
			return target.WorldPosition + Vector3.Up * (player.CurrentHeight - player.EyeDistanceFromTop);
		if ( target.GetComponent<Sandbox.Npcs.BaseNpc>() is { } npc ) return npc.EyePosition;
		return CalculateEyePosition( target );
	}

	internal bool CanSeeTargetEyes( GameObject target, bool submerged )
	{
		if ( !SharesWaterLevel( target, submerged ) ) return false;
		var trace = Scene.Trace.Ray( EyePosition, TargetEyePosition( target ) ).IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "npc", "player", "playercontroller", "trigger" ).Run();
		return !trace.Hit || trace.GameObject?.Root == target.Root;
	}

	internal static bool SharesWaterLevel( GameObject target, bool sourceSubmerged )
	{
		// A wading target can be reached from either side of the surface.
		var point = sourceSubmerged ? target.WorldPosition + Vector3.Up : TargetEyePosition( target );
		return WaterBody.Find( target.Scene, point ).IsValid() == sourceSubmerged;
	}

	internal static BBox TargetHullBounds( GameObject target )
	{
		if ( target.GetComponent<Npc>() is { } npc && npc._collisionHull.IsValid() )
			return npc._collisionHull.GetWorldBounds();
		if ( target.GetComponent<PlayerController>() is { } player )
			return player.BodyBox( MathF.Abs( player.WorldTransform.UniformScale ) ) + player.WorldPosition;
		if ( target.GetComponent<Rigidbody>() is { } body ) return body.GetWorldBounds();
		if ( target.GetComponent<Collider>() is { } collider ) return collider.GetWorldBounds();
		return target.GetBounds();
	}

	internal static Vector3 TargetHullCenter( GameObject target )
	{
		if ( target.GetComponent<Npc>() is { } npc ) return npc.HullCenter;
		if ( target.GetComponent<PlayerController>() is { } player )
			return target.WorldTransform.PointToWorld( Vector3.Up * (player.CurrentHeight * 0.5f) );
		return TargetHullBounds( target ).Center;
	}

	internal static Vector3 TargetBodyPosition( GameObject target )
	{
		if ( target.GetComponent<Npc>() is { } npc ) return npc.BodyTarget;
		var center = TargetHullCenter( target );
		if ( target.GetComponent<PlayerController>().IsValid() )
			return center + (TargetEyePosition( target ) - center) * Game.Random.Float( 0.5f, 1.1f );
		if ( target.GetComponent<Sandbox.Npcs.BaseNpc>() is { } actor )
			return Vector3.Lerp( center, actor.EyePosition, 0.25f );
		return center;
	}

	internal virtual Vector3 HullCenter => _collisionHull.IsValid() ? _collisionHull.WorldTransform.PointToWorld( _collisionHull.Center ) : WorldPosition;
	internal virtual Vector3 BodyTarget => Vector3.Lerp( HullCenter, EyePosition, 0.25f );

	internal virtual Vector3 AimAt( GameObject target ) => target.GetComponent<Npc>()?.BodyTarget
		?? Vector3.Lerp( target.WorldPosition, TargetEyePosition( target ), 0.65f );

	internal virtual bool CanHit( GameObject target, Vector3 position )
	{
		var hit = Scene.Trace.Ray( AimOrigin, position ).IgnoreGameObjectHierarchy( GameObject ).UseHitboxes().Run();
		return !hit.Hit || hit.GameObject?.Root == target.Root;
	}

	internal virtual void PlayAttackSound( Vector3 origin ) => _audio.Emit( Sounds.Attack, origin );

	internal virtual void Strike()
	{
		if ( !IsLiveTarget( _target ) )
			return;
		var end = AimAt( _target );
		var origin = AttackOrigin;
		var direction = (end - origin).Normal;
		SetAimBlend( _action, end - AimOrigin );
		if ( origin.Distance( end ) > StrikeRange + 24 || !CanHit( _target, end ) )
		{
			return;
		}

		if ( Definition.Attack != NpcAttack.Bullet && (IsFirstStrike || Definition.Attack == NpcAttack.Projectile) )
			PlayAttackSound( origin );
		switch ( Definition.Attack )
		{
			case NpcAttack.Bullet:
				FireBullet( _target, origin, end );
				break;
			case NpcAttack.Leap:
				_leapTarget = _target;
				_leapExpires = 0.8f;
				Launch( direction.WithZ( 0 ).Normal * 340 + Vector3.Up * 205 );
				break;
			case NpcAttack.Projectile:
				FireProjectile( _target, origin, direction );
				break;
			default:
				DealDamage( _target, origin, end, Definition.Attack switch
				{
					NpcAttack.Shock => "shock",
					_ => "melee"
				} );
				break;
		}
	}

	internal virtual void FireProjectile( GameObject target, Vector3 origin, Vector3 direction ) => NpcProjectile.Fire<NpcProjectile>( this, target, origin, direction );

	internal void EmitSound( string sample, Vector3 position, float volume = 1, float pitch = 1, VoicedNpc speaker = null, float attenuation = GoldSrcSoundEvents.Normal ) => _audio?.Emit( sample, position, volume, pitch, speaker, attenuation );

	internal Component TraceClaw( float reach, float amount, string tag, float pitch = 1 )
	{
		var victim = TraceMelee( reach, amount, tag, out var contact );
		EmitSound( contact ? "zombie/claw_strike" : "zombie/claw_miss", WorldPosition, pitch: pitch );
		return victim;
	}

	internal Component TraceMelee( float reach, float amount, string tag, out bool contact, string extraTag = null )
	{
		var half = new Vector3( 16, 16, 18 );
		// Keep short monsters' attack hulls clear of their supporting floor.
		var origin = WorldPosition + Vector3.Up * MathF.Max( Definition.Height * 0.5f, half.z + 0.5f );
		var hit = Scene.Trace.Box( new BBox( -half, half ), origin, origin + WorldRotation.Forward * reach )
			.IgnoreGameObjectHierarchy( GameObject ).UseHitboxes().Run();
		var victim = hit.GameObject?.GetComponentInParent<IDamageable>();
		contact = hit.Hit || hit.StartedSolid;
		if ( victim is null || amount <= 0 ) return victim as Component;
		var damage = new DamageInfo { Damage = amount, Origin = origin, Position = hit.EndPosition, Attacker = GameObject, Weapon = GameObject };
		damage.Tags.Add( tag );
		if ( extraTag is not null ) damage.Tags.Add( extraTag );
		victim.OnDamage( damage );
		return victim as Component;
	}

	internal static void PunchPlayer( PlayerController player, Vector3 push, Angles punch, Angles turn = default )
	{
		using ( Rpc.FilterInclude( player.Network.Owner ?? Connection.Local ) )
			HitPlayer( player, push, punch, turn );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void HitPlayer( PlayerController player, Vector3 push, Angles punch, Angles turn )
	{
		if ( !player.IsValid() || player.IsProxy ) return;
		if ( player.Body.IsValid() ) player.Body.Velocity += push;
		if ( turn != Angles.Zero )
			player.EyeAngles = new Angles( (player.EyeAngles.pitch + turn.pitch).Clamp( -89, 89 ), player.EyeAngles.yaw + turn.yaw, 0 );
		if ( !player.ThirdPerson && punch != Angles.Zero ) MeleeViewPunch.Apply( player.Scene.Camera, punch );
	}

	internal static void PushVictim( Component victim, Vector3 impulse )
	{
		if ( victim is Sandbox.Npcs.BaseNpc npc && npc.GetComponent<Rigidbody>() is { } body )
			npc.Launch( body.Velocity + impulse );
		else if ( victim?.GameObject.GetComponent<PlayerController>() is { } player && player.Body.IsValid() )
			PunchPlayer( player, impulse, Angles.Zero );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	internal static void EjectShell( Vector3 origin, Rotation facing, bool shotgun = false, string mountIdent = "hl1" )
	{
		var velocity = facing.Right * Game.Random.Float( 40, 90 ) + facing.Up * Game.Random.Float( 75, 200 )
			+ facing.Forward * Game.Random.Float( -40, 40 );
		ShellCasing.Eject( origin, facing, shotgun, velocity, Sandbox.Mounting.Directory.Get( mountIdent ) as GameMount );
	}

	internal virtual void FireBullet( GameObject target, Vector3 origin, Vector3 end )
	{
		EmitSound( Sounds.Attack, origin );
		DealDamage( target, origin, end, "bullet" );
	}

	void UpdateLeap()
	{
		if ( !IsLiveTarget( _leapTarget ) || _leapExpires <= 0 )
		{
			_leapTarget = null;
			return;
		}
		var point = AimAt( _leapTarget );
		if ( AttackOrigin.Distance( point ) > 48 || !CanHit( _leapTarget, point ) )
			return;
		DealDamage( _leapTarget, AttackOrigin, point, "melee" );
		_leapTarget = null;
	}

	internal virtual void DealDamage( GameObject victim, Vector3 origin, Vector3 position, string tag )
	{
		var damage = new DamageInfo { Damage = Definition.Damage, Attacker = GameObject, Weapon = GameObject, Origin = origin, Position = position };
		damage.Tags.Add( tag );
		if ( victim.GetComponentInParent<Component.IDamageable>() is { } damageable )
		{
			_audio.Emit( Sounds.Hit, position );
			damageable.OnDamage( damage );
		}
	}
}

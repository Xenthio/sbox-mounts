using System;
using GoldSrc.HalfLife;
using Sandbox.Npcs;
using Sandbox.Npcs.Layers;

namespace GoldSrc.CryOfFear;

[Title( "Flier" ), Category( "Cry of Fear" )]
[NpcDefinition( "monster_nerd", "flygare", MonsterClass.AlienMilitary, 60, 12, 72, 200,
	Mount = "cof", Attack = NpcAttack.Projectile, Damage = 15, Range = 2048, Movement = NpcMovement.Flying, Blood = Npc.BloodColor.Red )]
public sealed class Flier : VoicedNpc
{
	const float MeleeDamage = 10;
	const int SpitEvent = 2, LeftSlash = 3, RightSlash = 4, DownSlash = 5;
	static readonly string[] FlightClips = ["forward", "backward", "left", "right", "up", "down", "punch", "punchdown"];

	GameObject _spitTarget;
	int _spitAction = -1;
	TimeUntil _nextSpit;

	internal override float ViewDot => -0.7f;
	internal override string[] ExtraActions => FlightClips;
	internal override float VoicePitch => Game.Random.Int( 95, 105 ) / 100f;
	internal override NpcSounds Sounds { get; } = new( "flygare/flygare_idle", "flygare/flygare_alert",
		"flygare/flygare_pain.wav", "flygare/flygare_death.wav", "flygare/flygare_attack" );

	internal override void OnModelLoaded( ModelLoader loader ) => EyeHeight = -2;

	internal override void PlaceAtSpawn()
	{
		// World-space routes already carry altitude. Keep BSP placements at their authored height.
		if ( Movement is FlyingLayer flight ) flight.HoverHeight = 0;
	}

	internal override StudioSequence MovementSequence( float speed )
	{
		if ( speed < 20 ) return null;
		var local = WorldRotation.Inverse * Animation.MoveVelocity;
		var magnitudes = local.Abs();
		if ( magnitudes.x >= magnitudes.y && magnitudes.x >= magnitudes.z )
			return Find( local.x >= 0 ? "forward" : "backward" );
		if ( magnitudes.y >= magnitudes.z ) return Find( local.y >= 0 ? "left" : "right" );
		return Find( local.z >= 0 ? "up" : "down" );
	}

	internal override void Engage( GameObject target )
	{
		Animation.SetLookTarget( target );
		BeginAttack( target );
		if ( Action is not null )
		{
			Movement?.Stop();
			return;
		}

		var offset = target.WorldPosition - WorldPosition;
		var climb = offset.Normal.z;
		var speed = offset.Length < 128 ? 100 : 400 - climb * (climb >= 0 ? 100 : 600);
		// Approach from just above the victim so the downward swipe is available.
		Movement?.MoveTo( target.WorldPosition + Vector3.Up * 40, 6, speed, target );
	}

	internal override void BeginAttack( GameObject target )
	{
		var offset = target.WorldPosition - WorldPosition;
		var distance = offset.Length;
		var grounded = target.GetComponent<PlayerController>()?.IsOnGround ?? target.GetComponent<BaseNpc>()?.IsOnGround ?? false;
		if ( distance <= 64 && MathF.Abs( offset.z ) <= 32 && grounded && Find( "punch" ) is { } punch )
		{
			PlayAction( punch );
			return;
		}

		if ( offset.z is <= -32 and > -96 && distance <= 48 && Find( "punchdown" ) is { } down )
		{
			PlayAction( down );
			return;
		}

		var moving = Movement?.IsMoving == true;
		if ( distance <= 256 || distance > AttackRange || (moving && distance >= 512) || _nextSpit > 0 ||
			WorldRotation.Forward.Dot( offset.WithZ( 0 ).Normal ) <= 0.5f ||
			!CanSeeTargetEyes( target, false ) || Find( "shoot" ) is not { } shoot )
			return;

		_spitTarget = target;
		_nextSpit = moving ? 5 : 2;
		PlayAction( shoot );
		_spitAction = ActionSerial;
	}

	internal override void HandleActionEvent( int id )
	{
		if ( id == SpitEvent )
		{
			var target = _spitAction == ActionSerial ? _spitTarget : FindVisibleEnemy();
			if ( !IsLiveTarget( target ) ) return;
			EmitSound( Sounds.Attack, WorldPosition, pitch: VoicePitch, speaker: this );
			AcidSpit.Launch( this, target );
			return;
		}

		if ( id == DownSlash )
		{
			StrikeDown();
			return;
		}

		if ( id is not (LeftSlash or RightSlash) ) return;
		var hit = TraceMelee( 55, MeleeDamage, "slash", out _ );
		if ( hit?.GameObject.GetComponent<PlayerController>() is not { } player ) return;
		var side = id == LeftSlash ? -1 : 1;
		PunchPlayer( player, WorldRotation.Right * (side * 200) - WorldRotation.Up * 50, new Angles( 5, 0, side * 18 ) );
	}

	void StrikeDown()
	{
		var half = new Vector3( 16, 16, 36 );
		var trace = Scene.Trace.Box( new BBox( -half, half ), WorldPosition, WorldPosition - WorldRotation.Up * 96 )
			.IgnoreGameObjectHierarchy( GameObject ).UseHitboxes().Run();
		if ( trace.GameObject?.GetComponentInParent<IDamageable>() is not { } victim ) return;
		var damage = new DamageInfo
		{
			Damage = MeleeDamage, Attacker = GameObject, Weapon = GameObject,
			Origin = WorldPosition, Position = trace.EndPosition
		};
		damage.Tags.Add( "slash" );
		victim.OnDamage( damage );
	}

	internal override void ModifyDamage( Component.IDamageable.DamageEvent damage )
	{
		if ( HitGroups( damage.DamageInfo ).Has( "hitgroup_8" ) )
		{
			damage.AcceptedDamage = 0;
			EmitSound( "faster/faster_headhit", damage.DamageInfo.Position, pitch: VoicePitch );
			return;
		}
		base.ModifyDamage( damage );
	}

	internal override void PainSound()
	{
		if ( Game.Random.Int( 0, 5 ) < 2 ) EmitSound( Sounds.Pain, WorldPosition, pitch: VoicePitch, speaker: this );
	}
}

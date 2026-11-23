using System;
using GoldSrc.HalfLife;
using GoldSrc.Maps;
using Sandbox.Npcs;

namespace GoldSrc.TeamFortressClassic;

[Hide]
public sealed class FortressSentry : FortressBuilding
{
	const float SightRange = 1000;
	const float BaseHeight = 21.2f;
	internal const float Height = BaseHeight + 48;
	const float ThinkInterval = 0.1f;
	const int UpgradeCost = 130;
	const int RocketCapacity = 20;
	const int BulletDamage = 16;

	[Sync( SyncFlags.FromHost )] public int Level { get; private set; } = 1;
	[Sync( SyncFlags.FromHost )] public int Shells { get; private set; } = 25;
	[Sync( SyncFlags.FromHost )] public int Rockets { get; private set; }
	[Sync( SyncFlags.FromHost )] public Angles Aim { get; private set; }
	[Sync( SyncFlags.FromHost )] bool Firing { get; set; }

	GameObject _target;
	TimeUntil _nextThink, _nextShot, _nextRocket;
	int _scanDirection = 1, _visualLevel;
	MountedGun _gun;
	SkinnedModelRenderer _head;

	public override float MaxHealth => Level switch { 1 => 150, 2 => 180, _ => 216 };
	int MaxShells => Level switch { 1 => 100, 2 => 120, _ => 144 };
	Vector3 EyePosition => WorldPosition + Vector3.Up * (BaseHeight + 22);

	internal override void OnBuilt( BaseInventoryComponent inventory ) =>
		Mount?.NpcAudio.Emit( "weapons/turrset.wav", WorldPosition );

	internal override void Service( BaseInventoryComponent inventory, BaseAmmoResource cells )
	{
		if ( !IsBuilt || Health <= 0 ) return;
		if ( Level < 3 && inventory.GetAmmo( cells ) >= UpgradeCost && inventory.TakeAmmo( cells, UpgradeCost ) == UpgradeCost )
		{
			Level++;
			Health = MaxHealth;
			Mount?.NpcAudio.Emit( "weapons/turrset.wav", WorldPosition );
			return;
		}

		var cost = (int)MathF.Ceiling( (MaxHealth - Health) / 5 );
		var spent = inventory.TakeAmmo( cells, cost );
		if ( spent > 0 )
		{
			Health = MathF.Min( MaxHealth, Health + spent * 5 );
			Mount?.NpcAudio.Emit( "items/r_item2.wav", WorldPosition, attenuation: 0.8f );
			return;
		}

		Shells += inventory.TakeAmmo( GameResource.Load<BaseAmmoResource>( "mount://tfc/ammo/buckshot.ammo" ),
			Math.Min( 40, MaxShells - Shells ) );
		if ( Level == 3 )
			Rockets += inventory.TakeAmmo( GameResource.Load<BaseAmmoResource>( "mount://tfc/ammo/rockets.ammo" ),
				RocketCapacity - Rockets );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( !IsValid || !IsBuilt ) return;
		if ( _visualLevel != Level )
		{
			if ( LoadModel( $"sentry{Level}" ) is not { IsError: false } model ||
				Mount?.GetByPath( model.ResourcePath ) is not ModelLoader loader ) return;
			if ( !_head.IsValid() )
			{
				var head = new GameObject( GameObject, true, "Sentry head" ) { NetworkMode = NetworkMode.Never };
				head.LocalPosition = Vector3.Up * BaseHeight;
				_head = head.AddComponent<SkinnedModelRenderer>();
				_head.UseAnimGraph = false;
			}
			_head.Model = model;
			_head.Sequence.Looping = true;
			loader.EnsureNpcAnimations();
			_gun = new MountedGun();
			_gun.Bind( loader, _head );
			_visualLevel = Level;
		}
		var sequence = Firing ? "fire" : "spin";
		if ( _head.Sequence.Name != sequence ) _head.Sequence.Name = sequence;
		_gun?.Pose( _head, Aim );
	}

	internal override void OnBuildingUpdate()
	{
		if ( _nextThink > 0 ) return;
		_nextThink = ThinkInterval;

		if ( !CanSee( _target ) )
		{
			var target = FindTarget();
			if ( target.IsValid() && target != _target )
				Mount?.NpcAudio.Emit( "weapons/turrspot.wav", WorldPosition );
			_target = target;
		}

		if ( !_target.IsValid() )
		{
			Firing = false;
			if ( MathF.Abs( Aim.yaw ) >= 50 ) _scanDirection = Aim.yaw > 0 ? -1 : 1;
			TurnTo( new Angles( 0, _scanDirection * 50, 0 ), 4 );
			return;
		}

		var offset = Npc.TargetHullCenter( _target ) - EyePosition;
		var desired = (WorldRotation.Inverse * offset).EulerAngles.Normal;
		desired.pitch = desired.pitch.Clamp( -50, 50 );
		TurnTo( desired, 12 );
		var error = (desired - Aim).Normal;
		Firing = MathF.Abs( error.yaw ) + MathF.Abs( error.pitch ) <= 10 && (Shells > 0 || Rockets > 0);
		if ( !Firing || _nextShot > 0 ) return;

		_nextShot = Level == 1 ? 0.2f : 0.1f;
		Shoot();
	}

	void TurnTo( Angles desired, float step )
	{
		var delta = (desired - Aim).Normal;
		Aim = (Aim + new Angles( delta.pitch.Clamp( -step, step ), delta.yaw.Clamp( -step, step ), 0 )).Normal;
	}

	GameObject FindTarget()
	{
		var players = Scene.GetAllComponents<PlayerController>().Select( player => player.GameObject );
		var monsters = Scene.GetAllComponents<GoldSrc.HalfLife.Npc>()
			.Where( npc => Npc.ClassOf( npc.GameObject ) is MonsterClass.HumanMilitary or MonsterClass.AlienMilitary or
				MonsterClass.AlienMonster or MonsterClass.AlienPredator or MonsterClass.AlienPrey or MonsterClass.AlienWeapon )
			.Select( npc => npc.GameObject );
		var forward = (WorldRotation * Aim.Forward).WithZ( 0 ).Normal;
		return players.Concat( monsters )
			.Where( candidate => forward.Dot( (candidate.WorldPosition - WorldPosition).WithZ( 0 ).Normal ) > 0.7f && CanSee( candidate ) )
			.MinBy( candidate => candidate.WorldPosition.DistanceSquared( EyePosition ) );
	}

	bool CanSee( GameObject target )
	{
		if ( !target.IsValid() || !target.Enabled || target == Builder.GameObject ||
			target.WorldPosition.DistanceSquared( EyePosition ) > SightRange * SightRange ) return false;
		if ( target.GetComponent<PlayerController>() is { } player )
		{
			if ( MapState.PlayerVitals( player ) is not { Health: > 0 } ) return false;
		}
		else if ( target.GetComponent<BaseNpc>() is not { Health: > 0 } ) return false;

		if ( !Npc.SharesWaterLevel( target, WaterBody.Find( Scene, EyePosition ).IsValid() ) ) return false;
		var trace = Scene.Trace.Ray( EyePosition, Npc.TargetHullCenter( target ) ).IgnoreGameObjectHierarchy( GameObject )
			.IgnoreGameObjectHierarchy( target ).WithoutTags( "trigger", "playercontroller" ).Run();
		return !trace.Hit && !trace.StartedSolid;
	}

	void Shoot()
	{
		var barrel = Level == 3 && (Shells & 1) != 0 ? "attachment2" : "attachment1";
		var muzzle = _head?.GetAttachment( barrel )?.Position ?? EyePosition;
		var aimPoint = Npc.TargetHullCenter( _target );
		var direction = (aimPoint - muzzle).Normal;
		if ( Level == 3 && Rockets > 0 && _nextRocket <= 0 && Mount?.FindMountPath( "models/rpgrocket.mdl" ) is { } model )
		{
			var launcher = _head?.GetAttachment( (Rockets & 1) == 0 ? "attachment3" : "attachment4" )?.Position ?? muzzle;
			var rocket = NpcProjectile.Fire<SentryRocket>( Builder.GameObject, $"mount://tfc/{model}.vmdl", 100, null,
				launcher, (aimPoint - launcher).Normal, 800 );
			rocket.Sentry = GameObject;
			Rockets--;
			_nextRocket = 3;
			Mount?.NpcAudio.Emit( "weapons/rocketfire1.wav", launcher );
		}
		if ( Shells <= 0 ) return;
		Shells--;
		var hit = Scene.Trace.Ray( muzzle, muzzle + direction * 2048 ).WithCollisionRules( "bullet" ).UseHitboxes()
			.IgnoreGameObjectHierarchy( GameObject ).WithoutTags( "trigger", "playercontroller" ).Run();
		if ( hit.GameObject?.GetComponentInParent<IDamageable>() is { } victim && hit.GameObject.Root != Builder.GameObject.Root )
		{
			var damage = new DamageInfo { Damage = BulletDamage, Attacker = Builder.GameObject, Weapon = GameObject,
				Origin = muzzle, Position = hit.EndPosition, Hitbox = hit.Hitbox };
			damage.Tags.Add( "bullet" );
			victim.OnDamage( damage );
		}
		BulletEffects.Tracer( muzzle, hit.EndPosition );
		Mount?.NpcAudio.Emit( "weapons/pl_gun3.wav", muzzle, attenuation: 0.8f );
	}

	protected override void DestroyBuilding( GameObject attacker )
	{
		var origin = WorldPosition + Vector3.Up * 22;
		var damage = MathF.Min( 250, 25 + Shells * 0.75f + Rockets * 1.5f );
		FortressBlast.Apply( GameObject, Builder.IsValid() ? Builder.GameObject : attacker, origin, damage, falloff: 0.5f );
		Mount?.NpcAudio.Emit( "weapons/explode3.wav", origin );
		ShowExplosion( origin );
		GameObject.Destroy();
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void ShowExplosion( Vector3 origin )
	{
		if ( Mount is { } mount )
			Blast.SpriteEffect( "TFC sentry explosion", "explode01", origin, 3, 15, additive: false, mount: mount );
	}
}

[Hide]
public sealed class SentryRocket : FortressRocket
{
	internal GameObject Sentry { get; set; }
	internal override GameObject CollisionOwner => Sentry.IsValid() ? Sentry : base.CollisionOwner;
}

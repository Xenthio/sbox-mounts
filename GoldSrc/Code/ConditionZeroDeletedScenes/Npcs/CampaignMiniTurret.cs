using GoldSrc.HalfLife;
using GoldSrc.Maps;

namespace GoldSrc.ConditionZeroDeletedScenes;

[Title( "Mini Turret" ), Category( "Condition Zero Deleted Scenes" )]
[NpcDefinition( "monster_miniturret", "miniturret", MonsterClass.Machine, 30, 16, 32, 0, Mount = "czeror",
	Attack = NpcAttack.Bullet, Damage = 4, Range = 1200, Movement = NpcMovement.Stationary, AttackSequence = "fire", IdleSequence = "idle_off" )]
public sealed class CampaignMiniTurret : AutomatedTurret
{
	const int AutoActivateFlag = 32;
	const int StartInactiveFlag = 64;
	internal const string Model = "models/miniturret.mdl";

	[Property, Hide] public bool StartActive { get; set; } = true;
	[Property, Hide] public bool AutoActivate { get; set; } = true;
	[Property, Hide] public float MaxSleep { get; set; }

	bool? _powerOverride;
	internal override bool AutoDeploy => _powerOverride.HasValue ? _powerOverride.Value && AutoActivate : StartActive;
	internal override MonsterClass Classification => IsDeployed || AutoDeploy ? base.Classification : MonsterClass.None;
	internal override float SearchDuration => MaxSleep > 0 ? MaxSleep : base.SearchDuration;
	internal override float ShotDamage => System.Math.Clamp( SuitItem.Skill, 1, 3 ) + 2;
	internal override float ShotSpread => 0.08716f;

	internal override void ApplyMapProperties( Bsp.Entity entity )
	{
		var flags = entity.GetInt( "spawnflags" );
		AutoActivate = (flags & AutoActivateFlag) != 0;
		StartActive = AutoActivate && (flags & StartInactiveFlag) == 0;
		MaxSleep = entity.GetFloat( "maxsleep" );
		if ( entity.GetInt( "orientation" ) == 1 )
			WorldRotation *= Rotation.FromRoll( 180 );
	}

	internal override void PlaceAtSpawn()
	{
		base.PlaceAtSpawn();
		if ( Health == Definition.Health && SuitItem.Skill >= 3 )
			Health = 45;
	}

	internal void UsePower( bool? enabled )
	{
		if ( !Networking.IsHost || Health <= 0 ) return;
		_powerOverride = SetPower( enabled );
	}
}

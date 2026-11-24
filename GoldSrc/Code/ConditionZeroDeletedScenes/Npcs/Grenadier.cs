using System;
using GoldSrc.HalfLife;

namespace GoldSrc.ConditionZeroDeletedScenes;

[Hide]
public abstract class Grenadier : Rifleman
{
	const string ThrowSequence = "throwgrenade";
	const float ThrowInterval = 6;

	[Property, Hide] public bool HeGrenadeOnly { get; set; }
	[Property, Hide] public bool FlashbangOnly { get; set; }

	TimeUntil _nextGrenade;
	Vector3 _toss;

	internal override string[] ExtraActions => [.. base.ExtraActions, ThrowSequence];

	internal override void ApplyMapProperties( Bsp.Entity entity )
	{
		base.ApplyMapProperties( entity );
		HeGrenadeOnly = entity.GetInt( "hegrenadeonly" ) != 0;
		FlashbangOnly = entity.GetInt( "flashbangonly" ) != 0;
	}

	internal override void BeginAttack( GameObject target )
	{
		if ( !TryThrowGrenade( target ) ) base.BeginAttack( target );
	}

	internal override bool TryOccludedAttack( GameObject target ) => TryThrowGrenade( target );

	bool TryThrowGrenade( GameObject target )
	{
		if ( _nextGrenade > 0 || Movement?.IsMoving == true || Find( ThrowSequence ) is not { } clip ) return false;

		_nextGrenade = 1;
		var end = target.WorldPosition + Vector3.Up * 4;
		if ( (end - WorldPosition).WithZ( 0 ).LengthSquared <= 128 * 128 ) return false;
		if ( Scene.GetAllComponents<CampaignSoldier>().Any( other => other != this && other.Health > 0
			&& other.IsCounterTerrorist == IsCounterTerrorist && other.WorldPosition.DistanceSquared( end ) <= 256 * 256 ) )
			return false;

		var flightTime = Math.Clamp( AttackOrigin.Distance( end ) / 600, 1, 2 );
		if ( !TimedGrenade.TryAim( this, AttackOrigin, target, end, out _toss, flightTime ) ) return false;

		Movement?.Stop();
		PlayAction( clip );
		return true;
	}

	internal override void HandleActionEvent( int id )
	{
		if ( id == 7 && Action == ThrowSequence && !_toss.IsNearlyZero() )
		{
			var damage = IsCounterTerrorist ? 100 : UsesFlashbang() ? 0 : 85;
			CampaignGrenade.Throw( GameObject, AttackOrigin, _toss, damage );
			_toss = Vector3.Zero;
			_nextGrenade = ThrowInterval;
			return;
		}

		base.HandleActionEvent( id );
	}

	bool UsesFlashbang() => FlashbangOnly || (!HeGrenadeOnly && Game.Random.Int( 0, 99 ) <= 40);

	internal override SavedMagazine CaptureMagazine() =>
		base.CaptureMagazine() with { ShotDelay = MathF.Max( _nextGrenade, 0 ) };

	internal override void RestoreMagazine( SavedMagazine magazine )
	{
		base.RestoreMagazine( magazine );
		_nextGrenade = magazine.ShotDelay;
	}
}

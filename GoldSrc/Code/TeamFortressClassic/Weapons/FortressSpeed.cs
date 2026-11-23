using System;
using System.Runtime.CompilerServices;

namespace GoldSrc.TeamFortressClassic;

internal static class FortressSpeed
{
	static readonly ConditionalWeakTable<PlayerController, Limits> Active = new();

	internal static void SetAimLimit( PlayerController player, float? limit )
	{
		var limits = Active.GetValue( player, controller => new Limits( controller ) );
		limits.AimLimit = limit;
		Apply( player, limits );
	}

	internal static void SetTranquilized( PlayerController player, bool tranquilized )
	{
		var limits = Active.GetValue( player, controller => new Limits( controller ) );
		limits.Tranquilized = tranquilized;
		Apply( player, limits );
	}

	internal static void SetLegDamage( PlayerController player, float damage )
	{
		var limits = Active.GetValue( player, controller => new Limits( controller ) );
		limits.LegDamage = damage;
		Apply( player, limits );
	}

	static void Apply( PlayerController player, Limits limits )
	{
		player.WalkSpeed = limits.Walk.Apply( player.WalkSpeed, limits );
		player.RunSpeed = limits.Run.Apply( player.RunSpeed, limits );
		player.DuckedSpeed = limits.Ducked.Apply( player.DuckedSpeed, limits );
		if ( !limits.Tranquilized && limits.AimLimit is null && limits.LegDamage == 0 ) Active.Remove( player );
	}

	sealed class Limits( PlayerController player )
	{
		internal Speed Walk = new( player.WalkSpeed );
		internal Speed Run = new( player.RunSpeed );
		internal Speed Ducked = new( player.DuckedSpeed );
		internal bool Tranquilized;
		internal float? AimLimit;
		internal float LegDamage;
	}

	struct Speed( float initial )
	{
		float _original = initial;
		float _applied = initial;

		internal float Apply( float current, Limits limits )
		{
			if ( current != _applied ) _original = current;
			var scaled = limits.Tranquilized ? _original * 0.5f : _original;
			scaled *= 1 - limits.LegDamage * 0.1f;
			_applied = MathF.Min( scaled, limits.AimLimit ?? float.PositiveInfinity );
			return _applied;
		}
	}
}

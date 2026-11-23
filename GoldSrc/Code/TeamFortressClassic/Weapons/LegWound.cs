using System;

namespace GoldSrc.TeamFortressClassic;

[Hide]
public sealed class LegWound : Component
{
	const float MaximumDamage = 6;
	const float RecoveryDivisor = 20;

	[Property] public PlayerController Player { get; set; }
	[Sync( SyncFlags.FromHost )] internal float Damage { get; set; }
	float _lastHealth, _appliedDamage;

	internal static void Wound( PlayerController player, IDamageable vitals )
	{
		if ( player.GetComponentInChildren<LegWound>() is { } existing )
		{
			existing.CheckHealing( vitals );
			existing.Damage = MathF.Min( MaximumDamage, existing.Damage + 2 );
			return;
		}

		var go = new GameObject( player.GameObject, false, "TFC leg wound" );
		var wound = go.AddComponent<LegWound>();
		wound.Player = player;
		wound.Damage = 2;
		wound._lastHealth = vitals.Health;
		go.NetworkSpawn( true, null );
	}

	void CheckHealing( IDamageable vitals )
	{
		var health = vitals.Health;
		var healed = health - _lastHealth;
		_lastHealth = health;
		if ( healed <= 0 ) return;
		Damage = health >= vitals.MaxHealth - 5 ? 0 : MathF.Max( 0, Damage - (health + healed) / RecoveryDivisor );
		if ( Damage < 1 ) Damage = 0;
	}

	protected override void OnUpdate()
	{
		if ( Networking.IsHost )
		{
			var vitals = Player.IsValid() ? Maps.MapState.PlayerVitals( Player ) : null;
			if ( !Player.IsValid() || !Player.Enabled || vitals is null || vitals.Health <= 0 )
			{
				GameObject.Destroy();
				return;
			}

			CheckHealing( vitals );
			if ( Damage == 0 )
			{
				GameObject.Destroy();
				return;
			}
		}
		if ( !Player.IsValid() || (!Networking.IsHost && Player.IsProxy) || Damage == _appliedDamage ) return;
		_appliedDamage = Damage;
		FortressSpeed.SetLegDamage( Player, Damage );
	}

	protected override void OnDisabled()
	{
		if ( _appliedDamage != 0 && Player.IsValid() ) FortressSpeed.SetLegDamage( Player, 0 );
		_appliedDamage = 0;
	}
}

using System;
using GoldSrc.Maps;

namespace GoldSrc.TeamFortressClassic;

[Hide]
public sealed class MedikitOverheal : Component
{
	PlayerController _player;
	TimeUntil _nextDecay = 5;

	internal static void StartDecay( PlayerController player )
	{
		if ( player.GetComponentInChildren<MedikitOverheal>().IsValid() ) return;
		var effect = new GameObject( player.GameObject, true, "TFC bonus health" ) { NetworkMode = NetworkMode.Never };
		effect.AddComponent<MedikitOverheal>()._player = player;
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return;
		if ( !_player.IsValid() || !_player.Enabled || MapState.PlayerVitals( _player ) is not { Health: > 0 } vitals ||
			vitals.Health <= vitals.MaxHealth )
		{
			GameObject.Destroy();
			return;
		}

		if ( _nextDecay > 0 ) return;
		_nextDecay = 1;
		vitals.Health = MathF.Max( vitals.MaxHealth, vitals.Health - 1 );
	}
}

[Hide]
public sealed class MedikitInfection : Component
{
	const float SpreadRadius = 128;
	PlayerController _player;
	GameObject _attacker;
	TimeUntil _nextDamage = 2;

	internal static void Infect( PlayerController player, GameObject attacker )
	{
		if ( FortressPlayer.ClassOf( player ) == FortressClass.Medic ||
			FortressPlayer.AreTeammates( attacker?.GetComponent<PlayerController>(), player ) == true ) return;
		if ( player.GetComponentInChildren<MedikitInfection>().IsValid() ) return;
		var effect = new GameObject( player.GameObject, true, "TFC infection" ) { NetworkMode = NetworkMode.Never };
		var infection = effect.AddComponent<MedikitInfection>();
		infection._player = player;
		infection._attacker = attacker;
		if ( Sandbox.Mounting.Directory.Get( "tfc" ) is GameMount mount )
			mount.NpcAudio.Emit( "player/death2.wav", player.WorldPosition, attenuation: 0.8f );
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return;
		if ( !_player.IsValid() || !_player.Enabled || MapState.PlayerVitals( _player ) is not { Health: > 0 } vitals )
		{
			GameObject.Destroy();
			return;
		}

		if ( _nextDamage > 0 ) return;
		_nextDamage = 3;
		Spread();
		var damage = new DamageInfo { Damage = 8, Attacker = _attacker, Weapon = GameObject,
			Origin = WorldPosition, Position = WorldPosition };
		damage.Tags.Add( "poison" );
		vitals.OnDamage( damage );
	}

	void Spread()
	{
		var start = _player.WorldPosition;
		foreach ( var player in Scene.GetAllComponents<PlayerController>() )
		{
			if ( player == _player || !player.Enabled || (player.WorldPosition - start).LengthSquared > SpreadRadius * SpreadRadius ||
				MapState.PlayerVitals( player ) is not { Health: > 0 } || player.GetComponentInChildren<MedikitInfection>().IsValid() ) continue;

			var target = FortressHandGrenadeWeapon.HullCenter( player );
			var hit = Scene.Trace.Ray( FortressHandGrenadeWeapon.HullCenter( _player ), target )
				.IgnoreGameObjectHierarchy( _player.GameObject ).WithoutTags( "trigger" ).Run();
			if ( !hit.Hit || hit.GameObject?.GetComponentInParent<PlayerController>() == player )
				Infect( player, _attacker );
		}
	}
}

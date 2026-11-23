using GoldSrc.HalfLife;
using Sandbox.Npcs;

namespace GoldSrc.TeamFortressClassic;

[Hide]
public sealed class BurningPlayer : Component
{
	const float Duration = 5;
	const int MaximumStacks = 4;
	[Property] public PlayerController Player { get; set; }
	[Property] public Component Target { get; set; }
	[Property] public GameObject Attacker { get; set; }
	int _stacks = 1;
	TimeUntil _expires = Duration;
	TimeUntil _nextDamage = 1;
	SoundHandle _burning;

	internal static void Ignite( PlayerController player, Component target, GameObject attacker )
	{
		if ( WaterBody.Find( player.Scene, player.EyePosition ).IsValid() ) return;
		if ( player.GameObject.GetComponentInChildren<BurningPlayer>() is { } existing )
		{
			if ( existing._stacks >= MaximumStacks ) return;
			existing._stacks++;
			PlayIgnition( player );
			return;
		}

		var effect = new GameObject( player.GameObject, false, "TFC player fire" );
		effect.LocalPosition = Vector3.Up * 32;
		var fire = effect.AddComponent<BurningPlayer>();
		fire.Player = player;
		fire.Target = target;
		fire.Attacker = attacker;
		effect.NetworkSpawn( true, null );
		PlayIgnition( player );
	}

	static void PlayIgnition( PlayerController player )
	{
		if ( Sandbox.Mounting.Directory.Get( "tfc" ) is GameMount mount )
			mount.NpcAudio.Emit( "ambience/flameburst1.wav", player.WorldPosition );
	}

	protected override void OnStart()
	{
		if ( Application.IsDedicatedServer || Sandbox.Mounting.Directory.Get( "tfc" ) is not GameMount mount ) return;
		if ( mount.GetIndexedSprite( "playerflameloop" ) is { } sprite )
		{
			var renderer = GameObject.AddComponent<SpriteRenderer>();
			renderer.Flags |= ComponentFlags.NotSaved | ComponentFlags.NotNetworked;
			renderer.Sprite = sprite;
			renderer.Additive = true;
			var frame = sprite.Animations[0].Frames[0].Texture;
			renderer.Size = new Vector2( frame.Width, frame.Height ) * 0.5f;
		}
		if ( mount.NpcAudio.Find( "ambience/fire1.wav" ) is { } path )
			_burning = GoldSrcSoundEvents.Play( SoundFile.Load( path ), WorldPosition, attenuation: 0.5f );
	}

	protected override void OnUpdate()
	{
		if ( _burning.IsValid() ) _burning.Position = WorldPosition;
		if ( !Networking.IsHost ) return;
		if ( !Player.IsValid() || !Player.Enabled || !Target.IsValid() || Target is not IDamageable victim ||
			Target is BaseNpc { Health: <= 0 } || Maps.MapState.PlayerVitals( Player ) is { Health: <= 0 } ||
			WaterBody.Find( Scene, Player.EyePosition ).IsValid() )
		{
			GameObject.Destroy();
			return;
		}
		if ( _nextDamage <= 0 )
		{
			_nextDamage = 1;
			var info = new DamageInfo { Damage = _stacks * 2, Attacker = Attacker, Weapon = GameObject, Origin = WorldPosition, Position = WorldPosition };
			info.Tags.Add( "burn" );
			victim.OnDamage( info );
		}
		if ( _expires <= 0 ) GameObject.Destroy();
	}

	protected override void OnDisabled() => _burning?.Stop();
}

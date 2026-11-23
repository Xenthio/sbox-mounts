using GoldSrc.HalfLife;

namespace GoldSrc.CryOfFear;

[Title( "Baby" ), Category( "Cry of Fear" )]
[NpcDefinition( "monster_baby", "baby", MonsterClass.AlienMonster, 20, 4, 72, 184.26f,
	Mount = "cof", Attack = NpcAttack.Melee, Damage = 13, Range = 64, Blood = Npc.BloodColor.Red )]
public sealed class Baby : RushingMeleeNpc
{
	const int BurstEvent = 3;
	const float BurstRadius = 40;
	bool _burst;

	internal override string ChaseSequence => "walk";
	internal override float VoicePitch => Game.Random.Int( 95, 105 ) / 100f;
	internal override NpcSounds Sounds { get; } = new( Alert: "baby/b_alert", Pain: "baby/b_pain", Death: "baby/b_death" );

	internal override void OnModelLoaded( ModelLoader loader ) => EyeHeight = 28;

	internal override void HandleActionEvent( int id )
	{
		if ( id != BurstEvent || _burst ) return;
		_burst = true;
		var position = Renderer.GetAttachment( "attachment1" )?.Position ?? EyePosition;
		Body = 1;
		ApplyAppearance( Renderer.Model );
		EmitSound( "baby/b_attack", WorldPosition, pitch: VoicePitch, speaker: this );
		BloodEffects.Stream( Definition.Blood, position, GetVelocity( GameObject ), 120, Definition.Mount );

		foreach ( var player in Scene.GetAllComponents<PlayerController>().ToArray() )
		{
			if ( !player.IsValid() || player.GameObject.GetComponent<IDamageable>() is not { Health: > 0 } victim )
				continue;
			var point = TargetHullBounds( player.GameObject ).ClosestPoint( position );
			if ( point.DistanceSquared( position ) >= BurstRadius * BurstRadius ) continue;
			var damage = new DamageInfo
			{
				Damage = Definition.Damage, Attacker = GameObject, Weapon = GameObject,
				Origin = position, Position = point
			};
			damage.Tags.Add( "generic" );
			victim.OnDamage( damage );
		}

		if ( IsValid && Health > 0 )
			((IDamageable)this).OnDamage( new DamageInfo( Health, GameObject, GameObject ) { Position = position } );
	}

	internal override void PainSound()
	{
		if ( Game.Random.Int( 0, 5 ) < 2 ) EmitSound( Sounds.Pain, WorldPosition, pitch: VoicePitch, speaker: this );
	}

	internal override void DeathSound()
	{
		if ( Body != 1 ) base.DeathSound();
	}
}

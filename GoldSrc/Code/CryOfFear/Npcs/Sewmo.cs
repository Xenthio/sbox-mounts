using GoldSrc.HalfLife;

namespace GoldSrc.CryOfFear;

[Title( "Sewmo" ), Category( "Cry of Fear" )]
[NpcDefinition( "monster_sewmo", "sewmo", MonsterClass.AlienMonster, 60, 16, 72, 119,
	Mount = "cof", Attack = NpcAttack.Melee, Damage = 6, Range = 64, Blood = Npc.BloodColor.Red )]
public sealed class Sewmo : CryOfFearMeleeNpc
{
	const float EmergenceDistance = 128;
	const float ClawReach = 55;
	TimeUntil _nextProximityCheck;

	[Property] public bool EmergesOnApproach { get; set; }
	[Sync( SyncFlags.FromHost )] bool HasEmerged { get; set; }
	[Sync( SyncFlags.FromHost ), Change( nameof( ClawsChanged ) )] bool AlternateClaws { get; set; }

	bool WaitingToEmerge => EmergesOnApproach && !HasEmerged;
	internal override int EffectiveBody => AlternateClaws ? 1 : base.EffectiveBody;
	internal override NpcSounds Sounds { get; } = new( Alert: "sewmo/sewmo_alert", Pain: "sewmo/sewmo_pain" );
	internal override IEnumerable<StudioSequence> ExtraSequences =>
		[ ..base.ExtraSequences, Find( "attack2" ), Find( "appearidle" ), Find( "appear" ) ];

	internal override void ApplyMapProperties( Bsp.Entity entity ) => EmergesOnApproach = (entity.SpawnFlags & 32) != 0;

	internal override void PlaceAtSpawn()
	{
		base.PlaceAtSpawn();
		if ( WaitingToEmerge && Find( "appearidle" ) is { } idle ) PlayAction( idle );
	}

	protected override void Think()
	{
		if ( !WaitingToEmerge )
		{
			base.Think();
			return;
		}

		if ( Action == "appear" )
		{
			AdvanceAction();
			return;
		}

		if ( _nextProximityCheck <= 0 )
		{
			_nextProximityCheck = 0.1f;
			var nearby = Scene.GetAllComponents<PlayerController>().Any( player =>
				WorldPosition.DistanceSquared( player.WorldPosition ) <= EmergenceDistance * EmergenceDistance );
			if ( nearby && Find( "appear" ) is { } emergence )
			{
				EmitSound( "sewmo/out_water.wav", WorldPosition );
				PlayAction( emergence );
				return;
			}
		}

		if ( !AdvanceAction() && Find( "appearidle" ) is { } idle ) PlayAction( idle );
	}

	internal override void FinishAction( string name )
	{
		base.FinishAction( name );
		if ( name == "appear" ) HasEmerged = true;
	}

	internal override void ModifyDamage( Component.IDamageable.DamageEvent damage )
	{
		if ( WaitingToEmerge ) damage.AcceptedDamage = 0;
		else base.ModifyDamage( damage );
	}

	internal override StudioSequence SelectMeleeSequence( GameObject target ) => Find( EffectiveBody == 0 ? "attack1" : "attack2" );

	internal override void HandleActionEvent( int id )
	{
		if ( id == 6 )
		{
			AlternateClaws = true;
			return;
		}
		if ( id is not (1 or 2 or 3) ) return;

		var victim = TraceMelee( ClawReach, Definition.Damage, "slash", out _ );
		EmitSound( victim.IsValid() ? "sewmo/claw_strike" : $"sewmo/claw_miss{Game.Random.Int( 1, 2 )}.wav", WorldPosition,
			pitch: Game.Random.Int( 95, 105 ) / 100f );
		if ( Game.Random.Int( 0, 1 ) == 1 ) EmitSound( "sewmo/sewmo_attack", WorldPosition, speaker: this );

		if ( victim?.GameObject.GetComponent<PlayerController>() is { } player )
			PunchPlayer( player, WorldRotation.Right * (id == 2 ? 100 : -100), new Angles( 5, 0, id == 3 ? 0 : id == 2 ? 18 : -18 ) );
	}

	void ClawsChanged( bool before, bool after )
	{
		if ( Renderer.IsValid() && Renderer.Model.IsValid() ) ApplyAppearance( Renderer.Model );
	}
}

using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GoldSrc.Maps;

/// <summary>
/// Connects a BSP actor to map inputs and scripted sequences.
/// </summary>
[Category( "GoldSrc" )]
[Icon( "person" )]
public sealed class Monster : MapEntity
{
	[Property, Hide] public bool SpawnedByMaker { get; set; }
	[Property, Hide] public bool FromPreviousMap { get; set; }

	internal static Monster CloneSpawn( GameObject prefab, MapState map, Transform transform, string name )
	{
		var child = prefab.Clone( transform, startEnabled: false );
		child.SetParent( map.GameObject, true );
		child.Name = name;
		var actor = child.AddComponent<Monster>();
		actor.Map = map;
		actor.SpawnedByMaker = true;
		actor.Npc = child.GetComponent<HalfLife.Npc>( true );
		actor.Renderer = actor.Npc?.Renderer;
		return actor;
	}

	protected override void OnDestroy()
	{
		if ( (SpawnedByMaker || FromPreviousMap) && Map.IsValid() ) Map.Entities.Remove( this );
	}

	[Property] public HalfLife.Npc Npc { get; set; }
	[Property] public SkinnedModelRenderer Renderer { get; set; }

	/// <summary>
	/// The looping sequence the monster sits in when nothing else is driving it - its
	/// scripted_sequence's "m_iszIdle", or the model's own idle when there's no script.
	/// </summary>
	[Property] public string IdleSequence { get; set; }

	[Property] public string DamageTarget { get; set; }
	[Property] public string DeathTarget { get; set; }

	public override void Trigger( GameObject activator = null )
	{
		if ( Npc is ConditionZeroDeletedScenes.CampaignMiniTurret turret ) turret.UsePower( null );
		else base.Trigger( activator );
	}

	public override void TurnOn( GameObject activator = null )
	{
		if ( Npc is ConditionZeroDeletedScenes.CampaignMiniTurret turret ) turret.UsePower( true );
		else base.TurnOn( activator );
	}

	public override void TurnOff( GameObject activator = null )
	{
		if ( Npc is ConditionZeroDeletedScenes.CampaignMiniTurret turret ) turret.UsePower( false );
		else base.TurnOff( activator );
	}

	internal void ApplyTriggerProperties( Bsp.Entity entity )
	{
		var target = entity.GetString( "TriggerTarget" );
		switch ( entity.GetInt( "TriggerCondition" ) )
		{
			case 2: DamageTarget = target; break;
			case 4: DeathTarget = target; break;
		}
	}

	internal void FireDamageTarget()
	{
		if ( string.IsNullOrEmpty( DamageTarget ) ) return;
		var target = DamageTarget;
		DamageTarget = null;
		FireAnimationTarget( target );
	}

	internal void FireDeathTarget()
	{
		if ( string.IsNullOrEmpty( DeathTarget ) ) return;
		var target = DeathTarget;
		DeathTarget = null;
		FireAnimationTarget( target );
	}

	internal void FireAnimationTarget( string name, GameObject activator = null )
	{
		if ( Map.IsValid() ) Fire( Map.BuildOutput( name ), activator ?? GameObject );
	}

	protected override void OnStart()
	{
		if ( !Npc.IsValid() ) Play( IdleSequence );
	}

	/// <summary>
	/// Hold the monster in a named sequence, the way CCineMonster::StartSequence does. A model only
	/// carries the animations its own monster needed, so an unknown name falls back to an idle
	/// rather than leaving it in the bind pose.
	/// </summary>
	public void Play( string sequence )
	{
		if ( !Renderer.IsValid() || !Renderer.Model.IsValid() )
			return;

		var names = Renderer.Model.AnimationNames;

		if ( string.IsNullOrEmpty( sequence ) || !names.Contains( sequence ) )
			sequence = Idle( names );

		if ( sequence is null )
			return;

		Renderer.Sequence.Looping = true;
		Renderer.Sequence.Name = sequence;
	}

	/// <summary>
	/// An unscripted GoldSrc monster plays ACT_IDLE. Activities aren't carried through the built
	/// model, so the first idle-labelled sequence stands in - "idle1" on the humanoids, "idle01"
	/// on the G-Man, "idle2" on the forklift.
	/// </summary>
	private static string Idle( IReadOnlyList<string> names )
		=> names.FirstOrDefault( x => x.StartsWith( "idle", StringComparison.OrdinalIgnoreCase ) );
}

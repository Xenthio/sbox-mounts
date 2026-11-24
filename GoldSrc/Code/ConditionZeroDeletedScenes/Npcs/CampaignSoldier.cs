using System;
using GoldSrc.HalfLife;

namespace GoldSrc.ConditionZeroDeletedScenes;

[Hide]
public abstract class CampaignSoldier : Npc
{
	[Property, Hide] public int Head { get; set; } = -1;
	[Property, Hide] public bool HoldPosition { get; set; }
	[Property, Hide] public bool Wander { get; set; }
	[Property, Hide] public float WeaponRange { get; set; }
	[Property, Hide] public float WeaponAccuracy { get; set; }

	internal bool IsCounterTerrorist => Classification == MonsterClass.PlayerAlly;
	internal abstract int WeaponBodyGroup { get; }
	internal override float SightRange => 4096;
	internal override float ViewDot => IsCounterTerrorist ? 0.1f : -0.7f;
	internal override void OnModelLoaded( ModelLoader loader ) => EyeHeight = 55;

	internal override void ApplyMapProperties( Bsp.Entity entity )
	{
		Head = entity.GetInt( "head", -1 );
		var healthMultiplier = entity.GetFloat( "healthmult", 1 );
		if ( healthMultiplier > 0 ) Health *= healthMultiplier;
		WeaponRange = entity.GetFloat( "weaponrange" );
		WeaponAccuracy = entity.GetFloat( "weaponaccuracy" );
		Wander = entity.GetInt( "wander" ) != 0;
		HoldPosition = entity.GetInt( "cantmove" ) != 0;
		if ( HoldPosition ) HoldMapPosition();
	}

	protected float SpreadFor( float fallback ) => WeaponAccuracy > 0 ? MathF.Sin( WeaponAccuracy * MathF.PI / 360 ) : fallback;

	internal override string MapModel( Bsp.Entity entity ) =>
		entity.GetString( "monstermodel" ) is { Length: > 0 } model ? model : entity.GetString( "model" );

	internal override void ApplyEquipment()
	{
		var parts = Renderer.Model.Parts.All;
		var weapons = parts.FirstOrDefault( part => part.Name == "weapons" );
		if ( weapons is not null && WeaponBodyGroup < weapons.Choices.Count )
			Renderer.SetBodyGroup( weapons.Name, WeaponBodyGroup );

		var heads = parts.FirstOrDefault( part => part.Name == "heads" );
		if ( heads is not null && Head >= 0 && Head < heads.Choices.Count )
			Renderer.SetBodyGroup( heads.Name, Head );
	}

	internal override void IdleMovement()
	{
		if ( Wander && !HoldPosition ) base.IdleMovement();
	}

	internal override bool Pursue( Vector3 goal ) => !HoldPosition && base.Pursue( goal );
}

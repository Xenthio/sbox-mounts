using System;
using GoldSrc.HalfLife;
using GoldSrc.Maps;

namespace GoldSrc.ConditionZeroDeletedScenes;

[Hide]
public abstract class CampaignSoldier : Npc, Component.IPressable
{
	[Property, Hide] public int Head { get; set; } = -1;
	[Property, Hide] public bool HoldPosition { get; set; }
	[Property, Hide] public bool Wander { get; set; }
	[Property, Hide] public float WeaponRange { get; set; }
	[Property, Hide] public float WeaponAccuracy { get; set; }

	[Property, Hide] public string IdleClip { get; set; }
	[Property, Hide] public string UseTarget { get; set; }
	[Property, Hide] public bool Invulnerable { get; set; }

	internal bool IsCounterTerrorist => Classification == MonsterClass.PlayerAlly;
	internal abstract int WeaponBodyGroup { get; }
	internal override IEnumerable<StudioSequence> ExtraSequences =>
		[.. base.ExtraSequences, Find( RappelSequence ), Find( RappelAttack ), Find( RappelLanding ), Find( IdleClip )];
	internal override StudioSequence RestSequence => Find( IdleClip ) ?? base.RestSequence;
	internal override float SightRange => 4096;
	internal override float ViewDot => IsCounterTerrorist ? 0.1f : -0.7f;
	internal override void OnModelLoaded( ModelLoader loader ) => EyeHeight = 55;

	internal override void ApplyMapProperties( Bsp.Entity entity )
	{
		Head = entity.GetInt( "head", -1 );
		IdleClip = entity.GetString( "idleanim" );
		UseTarget = entity.GetString( "usetarget" );
		Invulnerable = entity.GetInt( "invulnerable" ) != 0;
		var healthMultiplier = entity.GetFloat( "healthmult", 1 );
		if ( healthMultiplier > 0 ) Health *= healthMultiplier;
		WeaponRange = entity.GetFloat( "weaponrange" );
		WeaponAccuracy = entity.GetFloat( "weaponaccuracy" );
		Wander = entity.GetInt( "wander" ) != 0;
		HoldPosition = entity.GetInt( "cantmove" ) != 0;
		if ( HoldPosition || entity.GetInt( "dontfall" ) != 0 ) HoldMapPosition();
	}

	bool CanUse => Health > 0 && !string.IsNullOrEmpty( UseTarget ) && UseTarget != "none";
	bool IPressable.CanPress( IPressable.Event e ) => CanUse;
	IPressable.Tooltip? IPressable.GetTooltip( IPressable.Event e ) => CanUse ? new( "Talk", "chat", null ) : null;

	bool IPressable.Press( IPressable.Event e )
	{
		if ( !CanUse ) return false;
		Use( e.Source.GameObject );
		return true;
	}

	[Rpc.Host]
	void Use( GameObject source )
	{
		var player = source?.GetComponentInParent<PlayerController>();
		if ( !CanUse || !player.IsValid() || player.WorldPosition.DistanceSquared( WorldPosition ) > 160 * 160 ||
			(Rpc.Caller is not null && (player.Network.Owner ?? Connection.Local) != Rpc.Caller) ) return;
		GetComponent<Monster>()?.FireAnimationTarget( UseTarget, player.GameObject );
	}

	internal override void ModifyDamage( Component.IDamageable.DamageEvent damage )
	{
		if ( Invulnerable ) damage.Cancelled = true;
		else base.ModifyDamage( damage );
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

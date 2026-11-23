using GoldSrc.HalfLife;

namespace GoldSrc.ConditionZeroDeletedScenes;

[Hide]
public abstract class Rifleman : Npc
{
	[Property, Hide] public int Head { get; set; } = -1;
	[Property, Hide] public float WeaponRange { get; set; }
	[Property, Hide] public bool HoldPosition { get; set; }

	internal abstract string RifleReport { get; }

	internal override float AttackRange => WeaponRange > 0 ? WeaponRange : base.AttackRange;
	internal override float StrikeRange => AttackRange;
	internal override float SightRange => 4096;
	internal override Vector3 AttackOrigin => EyePosition;

	internal override void ApplyMapProperties( Bsp.Entity entity )
	{
		Head = entity.GetInt( "head", -1 );
		WeaponRange = entity.GetFloat( "weaponrange" );
		HoldPosition = entity.GetInt( "cantmove" ) != 0;
		if ( HoldPosition ) HoldMapPosition();
	}

	internal override string MapModel( Bsp.Entity entity ) =>
		entity.GetString( "monstermodel" ) is { Length: > 0 } model ? model : entity.GetString( "model" );

	internal override void ApplyEquipment()
	{
		var model = Renderer.Model;
		var weapons = model.Parts.All.FirstOrDefault( part => part.Name == "weapons" );
		// The campaign rifle is entry four in both the terror and CT weapon bodygroups.
		const int Rifle = 4;
		if ( weapons is not null && weapons.Choices.Count > Rifle )
			Renderer.SetBodyGroup( weapons.Name, Rifle );

		var heads = model.Parts.All.FirstOrDefault( part => part.Name == "heads" );
		if ( heads is not null && Head >= 0 && Head < heads.Choices.Count )
			Renderer.SetBodyGroup( heads.Name, Head );
	}

	internal override void IdleMovement() { }
	internal override bool Pursue( Vector3 goal ) => !HoldPosition && base.Pursue( goal );

	internal override void FireBullet( GameObject target, Vector3 origin, Vector3 end )
	{
		var muzzle = Renderer.GetAttachment( "attachment1" )?.Position ?? origin;
		PlayGunshot( RifleReport, muzzle );
		TraceBullets( origin, end, 1, Definition.Damage, 0.05234f, AttackRange, 3 );
	}
}

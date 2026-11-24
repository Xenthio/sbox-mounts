using GoldSrc.HalfLife;

namespace GoldSrc.HalfLifeMMod;

[Title( "Tau Cannon" ), Category( "Half-Life: MMod" )]
[WeaponPickup( "gauss", "uranium", 100, Bucket = 3, Weight = 20, Mount = "hl1mmod" )]
public sealed class GaussGun : ClassicGaussGun
{
	bool _hasDeployed;

	internal override string DeploySequence => _hasDeployed ? base.DeploySequence : "draw_first";
	internal override string ShotSequence => "fire2";
	internal override string OverloadSound => "debris/beamstart14.wav";
	internal override float? IdleAfterShot => 1.5f;

	internal override (string Name, float? Delay) RestingAnimation =>
		Game.Random.Float( 0, 1 ) switch
		{
			<= 0.75f => ("idle", 4.07f),
			<= 0.875f => ("idle2", 4.07f),
			_ => ("fidget", 3.03f)
		};

	protected override void OnEquipped()
	{
		DeployTime = 0.5f;
		base.OnEquipped();
		_hasDeployed = true;
	}

	internal override void ApplyOverloadDamage() =>
		Blast.DamageInRadius( Owner.GameObject, Owner.GameObject, Owner.WorldPosition, 125, 312.5f, "shock", "alwaysgib" );
}

using GoldSrc.HalfLife;

namespace GoldSrc.CrackLife;

[Title( "9mm Assault Rifle" ), Category( "Crack-Life" )]
[WeaponPickup( "9mmar", "9mm", 999, Bucket = 2, Weight = 15, Mount = "cracklife" )]
public sealed class Rifle : CrackLifeRifle
{
	internal override float BulletDamage => 5;
}

[Title( "9mm Assault Rifle" ), Category( "Crack-Life: Campaign Mode" )]
[WeaponPickup( "9mmar", "9mm", 999, Bucket = 2, Weight = 15, Mount = "clcampaign" )]
public sealed class CampaignRifle : CrackLifeRifle
{
	internal override float BulletDamage => 8;
}

[Hide]
public abstract class CrackLifeRifle : ClassicRifle
{
	public override Ray AimRay => Owner.IsValid() ? Owner.EyeTransform.ForwardRay : base.AimRay;
	internal override int FreshRounds => 999;
	internal override string ShotSequence => $"shoot_{Game.Random.Int( 1, 3 )}";
	internal override string GrenadeModel => ContentMount is { } mount && mount.FindMountPath( "models/grenade.mdl" ) is { } path
		? $"mount://{mount.Ident}/{path}.vmdl"
		: null;
	internal override GameMount GrenadeEffectsMount => ContentMount;

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 999;
		SecondaryDelay = 0.1f;
		Ballistics = Ballistics with { Damage = BulletDamage };
	}
}

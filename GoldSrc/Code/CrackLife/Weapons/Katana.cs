using GoldSrc.HalfLife;

namespace GoldSrc.CrackLife;

[Title( "Katana" ), Category( "Crack-Life: Campaign Mode" )]
[WeaponPickup( "katana", null, 0, Mount = "clcampaign" )]
public sealed class Katana : ClassicCrowbar
{
	internal override string MeleeSample => "katana";
	internal override float HitDelay => 0.6f;
	internal override float SwingDamage( bool fresh ) => Ballistics.Damage;

	internal override void Configure()
	{
		base.Configure();
		Ballistics = Ballistics with { Damage = 85 };
		PrimaryDelay = 0.6f;
		SecondaryDelay = 2;
		SecondaryAutomatic = false;
	}

	public override void SecondaryAttack() => TossFedora( AimRay );

	[Rpc.Host( NetFlags.OwnerOnly )]
	void TossFedora( Ray ray )
	{
		if ( !IsActive || !Owner.IsValid() || !ray.Position.IsFinite || !ray.Forward.IsFinite ||
			ray.Position.Distance( Owner.EyePosition ) > 96 || _nextToss > 0 ) return;
		_nextToss = SecondaryDelay;
		NpcProjectile.Fire<Fedora>( Owner.GameObject, "mount://clcampaign/clcampaign/models/fedora.mdl.vmdl",
			0, null, ray.Position + ray.Forward * 15, ray.Forward, 512 );
		PlayToss();
	}

	TimeUntil _nextToss;

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void PlayToss() => PlaySample( "weapons/katana_tip.wav" );
}

[Hide]
public sealed class Fedora : NpcProjectile
{
	internal override float Lifetime => 8;

	internal override void Guide() => Velocity += Vector3.Down * (320 * Time.Delta);

	internal override bool Bounce( SceneTraceResult hit )
	{
		WorldPosition = hit.EndPosition + hit.Normal * 0.1f;
		Velocity = (Velocity - 2 * Velocity.Dot( hit.Normal ) * hit.Normal) * 0.6f;
		return true;
	}
}

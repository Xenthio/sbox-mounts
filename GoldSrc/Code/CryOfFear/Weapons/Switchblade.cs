using GoldSrc.HalfLife;

namespace GoldSrc.CryOfFear;

[Title( "Switchblade" ), Category( "Cry of Fear" )]
[WeaponPickup( "switchblade", null, 0, Bucket = 0, Weight = 1, ModelDirectory = "models/weapons/switchblade", Mount = "cof" )]
public sealed class Switchblade : MeleeWeapon
{
	const float GripChangeTime = 1.5f;

	[Sync] public bool ReverseGrip { get; private set; }

	internal override float StrikeDelay => 0.16f;
	internal override Angles StrikePunch => ReverseGrip ? new( -4, 1, 0 ) : new( 2, -5, 0 );
	internal override bool SwingSoundAtImpact => ReverseGrip;
	internal override bool MarksWalls => true;
	internal override string WorldModelPath => "police/models/weapons/switchblade/p_switchblade.mdl";
	internal override string DeploySequence => ReverseGrip ? "p2draw" : "p1draw";
	internal override (string Name, float? Delay) IdleAnimation => ReverseGrip ? ("p2idle", null) : ("p1idle", null);

	internal override void Configure()
	{
		base.Configure();
		HoldType = WeaponHolds.Knife;
		PrimaryDelay = 0.5f;
		SecondaryDelay = GripChangeTime;
		Ballistics = new() { Damage = 6, Range = 32, Radius = 0 };
	}

	protected override void OnEquipped()
	{
		base.OnEquipped();
		if ( !IsProxy ) PlayGripDrawSound();
	}

	[Rpc.Broadcast( NetFlags.OwnerOnly )]
	void PlayGripDrawSound() => PlaySample( ReverseGrip ? "weapons/switchblade/switchblade_draw2.wav" : "weapons/switchblade/switchblade_draw.wav" );

	public override bool CanSecondaryAttack() => NextPrimaryFire <= 0 && base.CanSecondaryAttack();

	public override void PrimaryAttack()
	{
		var variant = Game.Random.Int( 0, 2 ) == 0 ? 1 : 2;
		var prefix = ReverseGrip ? "p2" : "p1";
		var idleAfter = ReverseGrip ? 1.2f : 1;
		BeginSwing( $"{prefix}attack{variant}", ReverseGrip ? 9 : Ballistics.Damage, idleAfter );
		SetNextPrimaryFire( ReverseGrip ? 0.9f : PrimaryDelay );
		SetNextSecondaryFire( idleAfter );
	}

	public override void SecondaryAttack()
	{
		var sequence = ReverseGrip ? "p2top1" : "p1top2";
		ReverseGrip = !ReverseGrip;
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( sequence, idleAfter: GripChangeTime );
		SetNextFire( GripChangeTime );
	}
}

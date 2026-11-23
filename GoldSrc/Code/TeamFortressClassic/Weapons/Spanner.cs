using System;
using GoldSrc.HalfLife;
using GoldSrc.Maps;

namespace GoldSrc.TeamFortressClassic;

[Title( "Spanner" ), Category( "Team Fortress Classic" )]
[WeaponPickup( "tfc_spanner", "uranium", 200, EntityName = "tf_weapon_spanner", PickupModel = "p_spanner", Bucket = 0, Weight = 4, Mount = "tfc" )]
public sealed partial class Spanner : FortressMelee
{
	TimeUntil _nextRepair;

	internal override string WorldModelPath => "models/p_spanner.mdl";
	internal override (string Name, float? Delay) IdleAnimation => ("idle", 12.5f);
	internal override string SwingSequence( SceneTraceResult hit, Vector3 forward ) => hit.Hit ? "attack1" : "use";

	public override bool CanPrimaryAttack() => !IsBuilding && base.CanPrimaryAttack();
	public override bool CanSecondaryAttack() => !IsBuilding && IsActive && Owner.IsValid() && NextSecondaryFire <= 0;

	public override void PrimaryAttack()
	{
		SetNextFire( PrimaryDelay );
		base.PrimaryAttack();
	}

	// Sandbox has no TFC team assignment, so the second trigger selects the friendly action.
	public override void SecondaryAttack()
	{
		SetNextFire( PrimaryDelay );
		var aim = AimRay;
		var hit = TraceSwing( aim );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( SwingSequence( hit, aim.Forward ), idleAfter: 5 );
		PostWeaponEvent( WeaponEvents.Attack );
		Repair();
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void Repair()
	{
		if ( !IsActive || IsBuilding || !Owner.IsValid() || !Inventory.IsValid() || _nextRepair > 0 ) return;
		_nextRepair = PrimaryDelay;

		var targetObject = TraceSwing( AimRay ).GameObject;
		if ( targetObject?.GetComponentInParent<FortressDispenser>() is { } dispenser )
		{
			dispenser.Service( Inventory, PrimaryAmmoType );
			return;
		}

		var target = targetObject?.GetComponentInParent<PlayerController>();
		if ( !target.IsValid() || !target.Enabled || MapState.PlayerVitals( target ) is not { Health: > 0, MaxArmour: > 0 } vitals ) return;

		var cells = Math.Min( 10, Inventory.GetAmmo( PrimaryAmmoType ) );
		var capacity = cells * 5;
		var points = (int)MathF.Min( vitals.MaxArmour - vitals.Armour, capacity );
		if ( points <= 0 ) return;

		// TFC rounds the cell cost differently for a top-up that fills the remaining armour.
		var cost = points == capacity ? cells : Math.Min( cells, (int)MathF.Ceiling( points / 4f ) );
		if ( Inventory.TakeAmmo( PrimaryAmmoType, cost ) != cost ) return;
		vitals.Armour += points;
		ContentMount?.NpcAudio.Emit( "items/r_item2.wav", WorldPosition, attenuation: 0.8f );
	}
}

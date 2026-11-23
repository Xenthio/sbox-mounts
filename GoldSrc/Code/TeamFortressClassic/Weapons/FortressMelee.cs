using GoldSrc.HalfLife;

namespace GoldSrc.TeamFortressClassic;

[Title( "Crowbar" ), Category( "Team Fortress Classic" )]
[WeaponPickup( "tfc_crowbar", null, 0, EntityName = "tf_weapon_axe", PickupModel = "p_crowbar", Bucket = 0, Mount = "tfc" )]
public sealed class Crowbar : FortressMelee
{
	internal override string WorldModelPath => "models/p_crowbar.mdl";
}

[Title( "Umbrella" ), Category( "Team Fortress Classic" )]
[WeaponPickup( "umbrella", null, 0, EntityName = "tf_weapon_umbrella", PickupModel = "p_umbrella", Bucket = 0, Mount = "tfc" )]
public sealed class Umbrella : FortressMelee
{
	internal override string WorldModelPath => "models/p_umbrella.mdl";
}

[Title( "Knife" ), Category( "Team Fortress Classic" )]
[WeaponPickup( "tfc_knife", null, 0, EntityName = "tf_weapon_knife", PickupModel = "p_knife", Bucket = 0, Mount = "tfc" )]
public sealed class Knife : FortressMelee
{
	internal override string WorldModelPath => "models/p_knife.mdl";
	internal override bool UsesCrowbarSamples => false;
	internal override (string Name, float? Delay) IdleAnimation => (Game.Random.Int( 0, 1 ) == 0 ? "idle" : "idle2", 7.5f);

	internal override void Configure()
	{
		base.Configure();
		HoldType = WeaponHolds.Knife;
	}

	internal override float SwingDamage( GameObject target, Vector3 forward, bool usedHull )
	{
		if ( target?.GetComponentInParent<PlayerController>() is null ) return Ballistics.Damage;
		if ( !IsBackstab( target, forward ) ) return 40;
		return usedHull ? 120 : 60;
	}

	internal override string SwingSequence( SceneTraceResult hit, Vector3 forward ) =>
		IsBackstab( hit.GameObject, forward ) ? "stab" : "slash";

	static bool IsBackstab( GameObject target, Vector3 direction ) =>
		target.IsValid() && target.GetComponentInParent<PlayerController>() is { } player &&
		direction.WithZ( 0 ).Dot( player.EyeTransform.Forward.WithZ( 0 ) ) > 0;
}

[Hide]
public abstract class FortressMelee : CrowbarWeapon
{
	int _swing;
	TimeUntil? _decalDelay;
	Vector3 _impactPosition, _impactNormal;

	internal virtual bool UsesCrowbarSamples => true;
	internal override (string Name, float? Delay) IdleAnimation => ("idle1", null);

	internal override void Configure()
	{
		UsesAmmo = UsesClips = false;
		HoldType = WeaponHolds.Melee;
		PrimaryDelay = 0.4f;
		Ballistics = new() { Damage = 20, Range = 32, Radius = 0 };
		AttackDamageTags = ["club"];
	}

	public override bool CanSecondaryAttack() => false;

	public override void PrimaryAttack()
	{
		var aim = AimRay;
		var hit = TraceMelee( aim, Ballistics.Range, out var usedHull );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( SwingSequence( hit, aim.Forward ), idleAfter: 5 );
		if ( hit.Hit )
			ShootBullet( hit, SwingDamage( hit.GameObject, aim.Forward, usedHull ), 0, hitboxTags: false );

		ShootEffects( new ShotEffect( hit.EndPosition, hit.Hit, hit.Normal, hit.GameObject, hit.Surface, Origin: aim.Position ) );
	}

	internal virtual float SwingDamage( GameObject target, Vector3 forward, bool usedHull ) => Ballistics.Damage;

	internal virtual string SwingSequence( SceneTraceResult hit, Vector3 forward ) => hit.Hit
		? (_swing++ % 2 == 0 ? "attack2hit" : "attack3hit")
		: (_swing++ % 3) switch { 0 => "attack1miss", 1 => "attack2", _ => "attack3" };

	protected override void OnShootEffects( ShotEffect shot )
	{
		PostWeaponEvent( WeaponEvents.Attack );
		if ( Application.IsDedicatedServer ) return;
		if ( !shot.Hit )
		{
			if ( UsesCrowbarSamples )
				PlaySample( "weapons/cbar_miss1.wav", pitch: Game.Random.Int( 94, 109 ) / 100f );
			return;
		}
		if ( HitFlesh( shot ) )
		{
			if ( UsesCrowbarSamples )
				PlaySample( $"weapons/cbar_hitbod{Game.Random.Int( 1, 3 )}.wav" );
			return;
		}
		if ( ContentMount is not { } mount ) return;
		var material = mount.GetMaterialType( shot.Surface );
		if ( mount.GetSurfaceSound( material, "bullet" ) is { } impact ) Sound.Play( impact, shot.HitPosition );
		if ( UsesCrowbarSamples )
			PlaySample( $"weapons/cbar_hit{Game.Random.Int( 1, 2 )}.wav", pitch: Game.Random.Int( 98, 101 ) / 100f );
		_impactPosition = shot.HitPosition;
		_impactNormal = shot.Normal;
		_decalDelay = 0.2f;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( _decalDelay is not { } due || due > 0 ) return;
		_decalDelay = null;
		ImpactDecal.Place( _impactPosition, _impactNormal, $"shot{Game.Random.Int( 1, 5 )}", 10, ContentMount );
	}
}

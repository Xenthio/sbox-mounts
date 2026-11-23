using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Hide]
public abstract class DayOfDefeatFirearm : BulletWeapon
{
	int _tracerCount;

	internal virtual float StandingSpread => 0.035f;
	internal virtual bool EjectsBrass => true;
	internal virtual float MovingSpreadPenalty => 0.1f;
	internal virtual int BrassBodyGroup => 0;
	internal virtual float BrassDelay => 0;
	internal override Vector2 PelletSpread => Vector2.One * (StandingSpread + (Owner is { } player && player.Velocity.Length > 45 ? MovingSpreadPenalty : 0));

	TimeUntil? _brassDue;
	internal override (string Name, float? Delay) IdleAnimation => (Clip1 == 0 ? "empty_idle" : "idle", 2.5f);
	internal override float IdleAfterReloadStart => ReloadTime;

	internal override void Configure()
	{
		HoldType = WeaponHolds.Pistol;
		Handedness = WeaponHandedness.Right;
		PrimaryAutomatic = false;
		AutoReload = false;
		PrimaryDelay = 0.1f;
		AttackDamageTags = ["bullet", "nevergib"];
		Ballistics = new() { Damage = 40, Range = 8192, Radius = 0 };
	}

	public override void PrimaryAttack()
	{
		if ( Owner is { } player && WaterBody.Find( Scene, player.EyePosition ).IsValid() )
		{
			DryFire();
			return;
		}
		base.PrimaryAttack();
	}

	public override bool CanSecondaryAttack() => false;
	public override bool CanReload() => Ammo1 > Clip1 && base.CanReload();

	public override void DryFire()
	{
		PlaySample( "weapons/weaponempty.wav" );
		SetNextFire( 0.15f );
	}

	internal override string ShotSound => $"weapons/{Definition.Model}_shoot.wav";

	internal override void PlayShotSound() =>
		PlaySample( ShotSound, Game.Random.Float( 0.92f, 1 ), Game.Random.Int( 98, 101 ) / 100f );

	internal override void DrawShotTrail( Vector3 start, Vector3 end )
	{
		if ( _tracerCount++ % 3 == 0 ) base.DrawShotTrail( start, end );
	}

	protected override void OnShootEffects( ShotEffect shot )
	{
		base.OnShootEffects( shot );
		if ( shot.NoEvents || !EjectsBrass || Application.IsDedicatedServer ) return;
		if ( BrassDelay > 0 )
			_brassDue = BrassDelay;
		else
			EjectBrass();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( _brassDue is not { } due || due > 0 ) return;
		_brassDue = null;
		if ( IsActive ) EjectBrass();
	}

	protected override void OnHolstered()
	{
		_brassDue = null;
		base.OnHolstered();
	}

	void EjectBrass()
	{
		if ( Owner is not { } owner ) return;
		var eye = owner.EyeTransform;
		var model = !IsProxy && !owner.ThirdPerson ? ViewModel : WorldModel;
		var attachment = model?.GetComponent<SkinnedModelRenderer>()?.GetAttachment( "attachment2" );
		var origin = attachment?.Position ?? eye.PointToWorld( new Vector3( 20, -4, -12 ) );
		var velocity = eye.Rotation.Right * Game.Random.Float( 60, 80 ) + eye.Rotation.Up * Game.Random.Float( 60, 80 );
		ShellCasing.Eject( origin, eye.Rotation, false, velocity, ContentMount, "models/shells.mdl", BrassBodyGroup );
	}
}

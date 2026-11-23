using System;
using GoldSrc.HalfLife;

namespace GoldSrc.CryOfFear;

[Title( "Ruger P345" ), Category( "Cry of Fear" )]
[WeaponPickup( "p345", "p345", 80, Bucket = 4, Weight = 5, ModelDirectory = "models/weapons/p345", Mount = "cof" )]
public sealed class P345 : IronSightWeapon
{
	const float EmptyFireDelay = 1;
	const float AccuracyRecoveryInterval = 0.1f;
	static readonly float[] SpreadCones = [0, 0.00873f, 0.01745f, 0.02618f, 0.03490f, 0.04362f, 0.05234f, 0.06105f, 0.06976f, 0.07846f, 0.08716f];
	float _accuracyPenalty;
	TimeUntil _recoverAccuracy;
	bool _drawTracer = true;

	[Sync( SyncFlags.FromHost )] bool ReloadingFromEmpty { get; set; }

	internal override int PlayerMuzzleFlash => 1;
	internal override Vector2 PelletSpread => Vector2.One * SpreadCones[(int)_accuracyPenalty];
	internal override string WorldModelPath => "police/models/weapons/p345/p_p345.mdl";
	internal override string ShotSound => "weapons_new/p345/p345_fire.wav";
	internal override string DeploySequence => Clip1 == 0 ? "draw_noshot" : "draw";
	internal override string RaiseSightsSequence => Clip1 == 0 ? "iron_to_noshot" : "iron_to";
	internal override string LowerSightsSequence => Clip1 == 0 ? "iron_from_noshot" : "iron_from";
	internal override string ShotSequence => Zoomed
		? (Clip1 == 0 ? "iron_shoot_empty" : "iron_shoot")
		: (Clip1 == 0 ? "shoot_empty_1" : "shoot_1");
	internal override float? IdleAfterShot => 1;
	internal override (string Name, float? Delay) IdleAnimation => Zoomed
		? (Clip1 == 0 ? "iron_idle_noshot" : "iron_idle", null)
		: (Clip1 == 0 ? "idle1_noshot" : "idletest", null);
	internal override string ReloadSequence => ReloadingFromEmpty ? "reload_noshot" : "reload";
	internal override float IdleAfterReloadStart => ReloadingFromEmpty ? 3.16f : 2.73f;

	internal override void Configure()
	{
		HoldType = WeaponHolds.Pistol;
		Handedness = WeaponHandedness.Right;
		ClipMaxSize = 8;
		PrimaryAutomatic = false;
		PrimaryDelay = 0.2f;
		DeployTime = 1;
		ReloadTime = 2.73f;
		CanCancelReload = false;
		AttackDamageTags = ["bullet"];
		Ballistics = new() { Damage = 20, Range = 8192, Radius = 0 };
	}

	public override void PrimaryAttack()
	{
		if ( !HasPrimaryAmmo() ) return;
		if ( _recoverAccuracy <= 0 )
		{
			var recoveredSteps = 1 + MathF.Floor( -(float)_recoverAccuracy / AccuracyRecoveryInterval );
			_accuracyPenalty = MathF.Max( _accuracyPenalty - recoveredSteps, 0 );
		}

		base.PrimaryAttack();
		_accuracyPenalty = MathF.Min( _accuracyPenalty + 0.85f, 10 );
		_recoverAccuracy = 0.75f;
		if ( Clip1 == 0 ) SetNextPrimaryFire( EmptyFireDelay );
		if ( !IsProxy && Owner is { ThirdPerson: false } )
			MeleeViewPunch.Apply( Scene.Camera, new Angles( -3, 0, Game.Random.Int( -4, 4 ) ) );
	}

	public override void Reload()
	{
		if ( Networking.IsHost && !Zoomed && CanReload() )
		{
			ReloadingFromEmpty = Clip1 == 0;
			ReloadTime = IdleAfterReloadStart;
		}
		base.Reload();
	}

	protected override void OnReloadStarted()
	{
		base.OnReloadStarted();
		// Magazine changes discard any rounds left in the old magazine.
		if ( Networking.IsHost ) Clip1 = 0;
	}

	public override void DryFire()
	{
		if ( AutoReload && CanReload() )
		{
			Reload();
			return;
		}

		PlaySample( "weapons_new/p345/p345_click.wav" );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( Zoomed ? "iron_shoot_noshot" : "shoot_noshot" );
		SetNextFire( 0.5f );
	}

	internal override void DrawShotTrail( Vector3 start, Vector3 end )
	{
		if ( _drawTracer ) base.DrawShotTrail( start, end );
		_drawTracer = !_drawTracer;
	}

	protected override void OnShootEffects( ShotEffect shot )
	{
		base.OnShootEffects( shot );
		if ( !shot.NoEvents ) EjectShells( 1, false, "models/shell.mdl" );
	}
}

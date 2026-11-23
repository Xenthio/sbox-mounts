using System;
using System.Runtime.CompilerServices;
using GoldSrc.HalfLife;

namespace GoldSrc.CryOfFear;

[Hide]
public abstract class MagazinePistol : IronSightWeapon
{
	const float AccuracyRecoveryInterval = 0.1f;
	static readonly ConditionalWeakTable<PlayerController, HashSet<string>> DrawnWeapons = new();
	static readonly float[] SpreadCones = [0, 0.00873f, 0.01745f, 0.02618f, 0.03490f, 0.04362f, 0.05234f, 0.06105f, 0.06976f, 0.07846f, 0.08716f];
	float _accuracyPenalty;
	TimeUntil _recoverAccuracy;
	bool _drawTracer = true;

	[Sync( SyncFlags.FromHost )] bool ReloadingFromEmpty { get; set; }

	internal abstract float TacticalReloadTime { get; }
	internal abstract float EmptyReloadTime { get; }
	internal abstract float RecoilPerRound { get; }
	internal abstract float AccuracyRecoveryDelay { get; }
	internal virtual string EmptySound => "weapons_new/glock/weapon_fire_empty.wav";
	internal virtual float DryFireDelay => 0.5f;
	internal virtual float FirstDrawTime => 0;
	protected bool IsFirstDraw { get; private set; }

	internal override int PlayerMuzzleFlash => 1;
	internal override Vector2 PelletSpread => Vector2.One * SpreadCones[(int)_accuracyPenalty];
	internal override string ReloadSequence => ReloadingFromEmpty ? "reload_noshot" : "reload";
	internal override float IdleAfterReloadStart => ReloadingFromEmpty ? EmptyReloadTime : TacticalReloadTime;

	internal override void Configure()
	{
		HoldType = WeaponHolds.Pistol;
		Handedness = WeaponHandedness.Right;
		PrimaryAutomatic = false;
		DeployTime = 1;
		ReloadTime = TacticalReloadTime;
		AttackDamageTags = ["bullet"];
	}

	protected override void OnEquipped()
	{
		if ( FirstDrawTime > 0 && Owner.IsValid() )
		{
			IsFirstDraw = DrawnWeapons.GetOrCreateValue( Owner ).Add( Definition.Model );
			DeployTime = IsFirstDraw ? FirstDrawTime : 29f / 30;
		}

		base.OnEquipped();
	}

	protected bool FireRound()
	{
		if ( !HasPrimaryAmmo() ) return false;
		if ( _recoverAccuracy <= 0 )
		{
			var recoveredSteps = 1 + MathF.Floor( -(float)_recoverAccuracy / AccuracyRecoveryInterval );
			_accuracyPenalty = MathF.Max( _accuracyPenalty - recoveredSteps, 0 );
		}

		base.PrimaryAttack();
		_accuracyPenalty = MathF.Min( _accuracyPenalty + RecoilPerRound, 10 );
		_recoverAccuracy = AccuracyRecoveryDelay;
		return true;
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

		PlaySample( EmptySound );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( Zoomed ? "iron_shoot_noshot" : "shoot_noshot" );
		SetNextFire( DryFireDelay );
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

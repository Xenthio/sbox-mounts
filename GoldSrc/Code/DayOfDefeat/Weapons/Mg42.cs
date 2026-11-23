using System;
using GoldSrc.HalfLife;

namespace GoldSrc.DayOfDefeat;

[Title( "MG42" ), Category( "Day of Defeat" )]
[WeaponPickup( "mg42", "ammo_66mm_ger", 500, Bucket = 2, Weight = 40, Mount = "dod" )]
public sealed class Mg42 : BeltFedMachineGun
{
	const float CoolingInterval = 0.1f;
	const float WarningHeat = 95;
	const float LimitHeat = 99;

	float _heat;
	TimeUntil _nextCool;
	TimeUntil _smokeEnds;

	internal override int FreshPickupRounds => 500;
	internal override float CarriedReloadTime => 6.5f;
	internal override string BrassModel => "models/shell_mg42.mdl";
	internal override bool EjectsBrass => true;
	internal override int BrassBodyGroup => 0;
	internal override Vector2 PelletSpread => BipodDeployed ? Vector2.One * 0.025f : base.PelletSpread;

	internal override void Configure()
	{
		base.Configure();
		ClipMaxSize = 250;
	}

	public override bool CanPrimaryAttack() => _heat < LimitHeat && base.CanPrimaryAttack();

	public override void PrimaryAttack()
	{
		if ( _heat >= LimitHeat ) return;
		if ( Owner is { } player && WaterBody.Find( Scene, player.EyePosition ).IsValid() )
		{
			base.PrimaryAttack();
			return;
		}

		var rounds = !IsHeld || (Clip1 >= 2 && TakePrimaryAmmo()) ? 2 : 1;
		Ballistics = Ballistics with { Pellets = rounds };
		base.PrimaryAttack();
		var previousHeat = _heat;
		_heat += 3;
		_nextCool = CoolingInterval;
		if ( previousHeat < WarningHeat && _heat >= WarningHeat ) ShowOverheat();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( IsProxy || _heat <= 0 || _nextCool > 0 ) return;

		var ticks = 1 + MathF.Floor( -_nextCool.Relative / CoolingInterval );
		_heat = MathF.Max( 0, _heat - 0.6f * ticks );
		_nextCool = _nextCool.Relative + CoolingInterval * ticks;
	}

	[Rpc.Broadcast( NetFlags.OwnerOnly )]
	async void ShowOverheat()
	{
		if ( Application.IsDedicatedServer || _smokeEnds > 0 ) return;
		PlaySample( "weapons/mgoverheat.wav" );
		_smokeEnds = 2.5f;
		await GameTask.DelaySeconds( 0.5f );
		while ( IsValid && IsActive && _smokeEnds > 0 )
		{
			var smoke = Blast.SpriteEffect( "MG42 barrel smoke", "puff", GetMuzzleTransform().Position, 0.4f, 4,
				additive: false, mount: ContentMount );
			if ( !smoke.IsValid() ) return;
			smoke.GetComponent<SpriteRenderer>().Color = Color.White.WithAlpha( 180f / 255 );
			smoke.AddComponent<RisingSmoke>();
			await GameTask.DelaySeconds( 0.2f );
		}
	}
}

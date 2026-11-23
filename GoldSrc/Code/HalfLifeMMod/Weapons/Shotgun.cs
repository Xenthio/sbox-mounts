using GoldSrc.HalfLife;

namespace GoldSrc.HalfLifeMMod;

[Title( "Shotgun" ), Category( "Half-Life: MMod" )]
[WeaponPickup( "shotgun", "buckshot", 125, Bucket = 2, Weight = 15, Mount = "hl1mmod" )]
public sealed class Shotgun : PumpShotgun
{
	const float MinimumSpread = 0.06105f;

	internal override (float Damage, int Pellets) ShellDamage => (5, 6);
	internal override Vector2 PelletSpread => Vector2.One * _spread;
	internal override string DeploySequence => _hasDeployed ? "draw" : "deploy_first";

	TimeUntil _firstShell, _secondShell;
	int _shellsToEject;
	bool _hasDeployed;
	float _spread = MinimumSpread;

	internal override void Configure()
	{
		base.Configure();
		SecondaryDelay = 1.4f;
	}

	protected override void OnEquipped()
	{
		DeployTime = _hasDeployed ? 0.5f : 2.2f;
		_spread = MinimumSpread;
		base.OnEquipped();
		_hasDeployed = true;
	}

	internal override void OnShellsFiring( bool both ) =>
		_spread = System.MathF.Min( both ? 0.08716f : 0.07846f, _spread + 0.003f );

	protected override void Think()
	{
		base.Think();
		if ( !Input.Down( "attack1" ) && !Input.Down( "attack2" ) )
			_spread = System.MathF.Max( MinimumSpread, _spread - 0.001f );
	}

	internal override void EjectSpentShells( bool both )
	{
		// Match the ejection frames of shoot and shoot_big.
		_shellsToEject = both ? 2 : 1;
		_firstShell = both ? 22f / 26 : 12f / 20;
		_secondShell = 23f / 26;
	}

	internal override void PlayPump()
	{
		// The first-person sequences already play both pump samples.
		if ( !ViewModel.IsValid() )
			PlaySample( "weapons/scock_back1.wav" );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( _shellsToEject == 2 && _firstShell <= 0 )
		{
			EjectShells( 1, true );
			_shellsToEject = 1;
			_firstShell = _secondShell;
		}
		if ( _shellsToEject == 1 && _firstShell <= 0 )
		{
			EjectShells( 1, true );
			_shellsToEject = 0;
		}
	}

	protected override void OnHolstered()
	{
		_shellsToEject = 0;
		base.OnHolstered();
	}
}

using System;

namespace GoldSrc.CounterStrike;

[Hide]
public abstract class CounterStrikeSmg : CounterStrikeAutomatic
{
	internal virtual bool QuadraticAccuracy => false;
	internal virtual bool IntegerAccuracyDivision => true;
	internal override float MovingSpreadSpeed => float.PositiveInfinity;
	internal override float MovingPenalty => 0;
	internal override float ReloadAccuracy => InitialAccuracy;
	internal override string BrassModel => "models/pshell.mdl";
	internal override float? IdleAfterShot => 2;

	internal override void Configure()
	{
		base.Configure();
		HoldType = WeaponHolds.Smg;
	}

	internal override float FiringAccuracy( int shots )
	{
		var power = QuadraticAccuracy ? shots * shots : shots * shots * shots;
		var growth = IntegerAccuracyDivision ? power / (int)AccuracyDivisor : power / AccuracyDivisor;
		return MathF.Min( MaxAccuracy, growth + AccuracyOffset );
	}
}

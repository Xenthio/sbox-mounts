namespace GoldSrc.CounterStrike;

[Hide]
public sealed class FlashbangDisplay : Component
{
	TimeSince _elapsed;
	float _fade, _hold, _alpha;

	internal float Opacity => _alpha * ((_fade + _hold - _elapsed) / _fade).Clamp( 0, 1 );

	internal void Apply( float fade, float hold, bool facingFlash )
	{
		var alpha = facingFlash ? 1 : 200f / 255;
		if ( Enabled && _elapsed < _hold + _fade )
		{
			fade = System.MathF.Max( fade, _fade );
			alpha = System.MathF.Max( alpha, _alpha );
			if ( facingFlash ) hold += System.MathF.Max( 0, _hold - _elapsed );
		}
		_fade = fade;
		_hold = hold;
		_alpha = alpha;
		_elapsed = 0;
		Enabled = true;
	}

	protected override void OnUpdate()
	{
		if ( _elapsed >= _fade + _hold )
		{
			Enabled = false;
			return;
		}
		if ( Scene.Camera is not { IsValid: true } camera ) return;
		using var painter = camera.BeginHud();
		painter.BlendMode = BlendMode.Normal;
		painter.Fill = Color.White.WithAlpha( Opacity );
		painter.Rect( new Rect( Vector2.Zero, camera.ScreenRect.Size ) );
	}
}

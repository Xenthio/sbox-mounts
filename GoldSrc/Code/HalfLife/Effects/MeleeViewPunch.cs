using System.Runtime.CompilerServices;

namespace GoldSrc.HalfLife;

internal sealed class MeleeViewPunch : CameraEffectSystem.BaseEffect
{
	static readonly ConditionalWeakTable<CameraComponent, MeleeViewPunch> Active = new();
	Angles _angles;

	internal static Angles Current( CameraComponent camera ) =>
		camera.IsValid() && Active.TryGetValue( camera, out var punch ) ? punch._angles : Angles.Zero;

	internal static void Apply( CameraComponent camera, Angles hit )
	{
		if ( !camera.IsValid() ) return;
		if ( Active.TryGetValue( camera, out var previous ) )
		{
			// The SDK only assigns the axes affected by this attack.
			if ( hit.pitch == 0 ) hit.pitch = previous._angles.pitch;
			if ( hit.yaw == 0 ) hit.yaw = previous._angles.yaw;
			if ( hit.roll == 0 ) hit.roll = previous._angles.roll;
			previous.Stop();
			Active.Remove( camera );
		}

		var punch = new MeleeViewPunch { Camera = camera, Duration = 0, _angles = hit };
		Active.Add( camera, punch );
		camera.Scene.GetSystem<CameraEffectSystem>().Add( punch );
	}

	public override void Update( float delta )
	{
		var length = _angles.AsVector3().Length;
		var remaining = length - (10 + length * 0.5f) * delta;
		if ( remaining > 0 )
		{
			_angles *= remaining / length;
			return;
		}

		_angles = Angles.Zero;
		Stop();
		if ( Active.TryGetValue( Camera, out var current ) && current == this ) Active.Remove( Camera );
	}

	public override void Evaluate( float scale, ref Vector3 position, ref Angles angles, ref float fieldOfView )
	{
		if ( !IsDone ) angles += _angles * scale;
	}
}

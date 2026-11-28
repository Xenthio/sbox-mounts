namespace UnityMount;

/// <summary>Applies the local Euler rotation increment stored by a mounted Unity prefab.</summary>
[Hide]
public sealed class UnityLocalSpin : Component
{
	[Property] public Vector3 Speed { get; set; }
	[Property] public bool RandomizeSpeed { get; set; }
	[Property] public Vector3 MinimumSpeed { get; set; }

	Vector3 _speed;

	protected override void OnStart()
	{
		_speed = Speed;
		if ( !RandomizeSpeed ) return;
		var random = Game.Random;
		_speed = new Vector3( float.Lerp( MinimumSpeed.x, Speed.x, random.Float() ),
			float.Lerp( MinimumSpeed.y, Speed.y, random.Float() ),
			float.Lerp( MinimumSpeed.z, Speed.z, random.Float() ) );
	}

	protected override void OnUpdate() =>
		LocalRotation *= UnityCoordinates.EulerRotation( _speed * Time.Delta );
}

using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

public sealed partial class ReflectionLaser
{
	readonly List<LineRenderer> _lines = [];
	SoundHandle _loop;
	bool _presenting;

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void BeginPresentation()
	{
		_presenting = true;
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( "spin", holdLastFrame: true );
		PostWeaponEvent( WeaponEvents.Attack );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowBeams( Vector3[] endpoints, bool cutting )
	{
		if ( Application.IsDedicatedServer || !_presenting || ContentMount is not { } mount ) return;
		_loop ??= PlaySample( "ambience/screammachine.wav", 0.98f, 1.25f, 0.8f );
		var count = endpoints.Length / 2;
		var muzzle = GetMuzzleTransform().Position;
		var material = mount.GetTrailMaterial( cutting ? "cutbeam" : "laserbeam" );
		var color = cutting ? Color.White.WithAlpha( 35f / 255 ) : new Color( 64f / 255, 192f / 255, 1, 10f / 255 );
		for ( int i = 0; i < count; ++i )
		{
			if ( i == _lines.Count )
			{
				var go = new GameObject( GameObject, false, "Reflection Laser beam" ) { NetworkMode = NetworkMode.Never };
				var line = go.AddComponent<LineRenderer>();
				line.UseVectorPoints = true;
				line.VectorPoints = [Vector3.Zero, Vector3.Zero];
				line.Additive = true;
				line.Opaque = line.CastShadows = false;
				_lines.Add( line );
			}

			var beam = _lines[i];
			var start = endpoints[i * 2];
			var end = endpoints[i * 2 + 1];
			beam.VectorPoints[0] = start == endpoints[0] ? muzzle : start;
			beam.VectorPoints[1] = end;
			beam.Width = cutting ? 40 : 20;
			beam.Color = color;
			beam.Texturing = TrailTextureConfig.Default with { Material = material, WorldSpace = true, UnitsPerTexture = 128 };
			beam.GameObject.Enabled = true;
		}

		for ( int i = count; i < _lines.Count; ++i )
			_lines[i].GameObject.Enabled = false;
	}

	void UpdatePresentation()
	{
		if ( _loop.IsValid() ) _loop.Position = GetMuzzleTransform().Position;
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void EndPresentation( float delay )
	{
		StopPresentation();
		SetNextFire( delay );
		var view = ViewModel?.GetComponent<StudioWeaponView>();
		if ( view.IsValid() ) view.RemainingIdleTime = 2;
	}

	void StopPresentation()
	{
		_presenting = false;
		_loop?.Stop();
		_loop = null;
		foreach ( var line in _lines )
			if ( line.IsValid() ) line.GameObject.Destroy();
		_lines.Clear();
	}
}

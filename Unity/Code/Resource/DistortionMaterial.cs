namespace UnityMount;

sealed class UnityDistortionMaterialLoader( UnityDistortionMaterial source, UnityMountedTexture normal, UnityMountedTexture mask ) : ResourceLoader<GameMount>
{
	protected override object Load()
	{
		var material = Material.Create( Path, "shaders/unity/particle_distortion.shader" );
		if ( normal.Path is not null ) material.Set( "g_tNormal", Texture.Load( normal.Path ) );
		material.Set( "g_tMask", mask.Path is null ? Texture.White : Texture.Load( mask.Path ) );
		material.Set( "g_vNormalST", source.Normal.Transform );
		material.Set( "g_vMaskST", source.Mask.Transform );
		material.Set( "g_vSamplers", new Vector4( normal.SamplerIndex, mask.SamplerIndex, normal.MirrorV, mask.MirrorV ) );
		material.Set( "g_flMagnitude", source.Magnitude );
		material.Set( "g_vCameraFade", source.CameraFade );
		material.Set( "g_flInverseFade", source.InverseFade );
		material.Set( "g_flDistanceModulation", source.DistanceModulation );
		material.Set( "g_flInchesPerMeter", UnityCoordinates.ToInches( 1f ) );
		return material;
	}
}

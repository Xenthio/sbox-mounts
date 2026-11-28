namespace UnityMount;

readonly record struct UnitySceneSky( string Material, UnitySky Source )
{
	public void AddToScene()
	{
		var material = Sandbox.Material.Load( Material );
		if ( material is null || material.IsError ) return;
		var gameObject = new GameObject( true, "Skybox" )
		{
			WorldRotation = Rotation.FromYaw( Source.Rotation ),
			IsStatic = true
		};
		var sky = gameObject.AddComponent<SkyBox2D>( false );
		sky.SkyMaterial = material;
		sky.Tint = Source.GetLinearTint();
		sky.SkyIndirectLighting = false;
		sky.Enabled = true;
	}
}

sealed class UnitySkyMaterialLoader( string cubemap ) : ResourceLoader<GameMount>
{
	protected override object Load()
	{
		var texture = Texture.Load( cubemap );
		if ( texture is null || texture.IsError ) return null;
		var material = Material.Create( Path, "shaders/sky.shader", false );
		if ( material is null || material.IsError ) return null;
		material.Set( "g_tSkyTexture", texture );
		material.Set( "SkyTexture", texture );
		return material;
	}
}

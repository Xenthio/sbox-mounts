namespace UnityMount;

sealed record UnityPrefabLine( UnityLine Source, string[] Materials )
{
	public void Create( GameObject target )
	{
		var materials = Materials.Select( Material.Load ).Where( material => material.IsValid() ).ToArray();
		if ( materials.Length == 0 ) return;

		var positions = Source.Positions.ToList();
		if ( Source.Loop && positions[0] != positions[^1] ) positions.Add( positions[0] );
		var points = Source.WorldSpace ? null : positions.Select( (position, index) =>
			new GameObject( target, true, $"Line point {index + 1}" ) { LocalPosition = position } ).ToList();

		var cap = Source.RoundCaps && !Source.Loop ? SceneLineObject.CapStyle.Rounded : SceneLineObject.CapStyle.None;
		foreach ( var material in materials )
		{
			var renderer = target.AddComponent<LineRenderer>( false );
			renderer.UseVectorPoints = Source.WorldSpace;
			renderer.VectorPoints = Source.WorldSpace ? positions : null;
			renderer.Points = points;
			renderer.Face = SceneLineObject.FaceMode.Camera;
			renderer.Width = Source.HalfWidth;
			renderer.Color = Source.Color;
			renderer.StartCap = cap;
			renderer.EndCap = cap;
			renderer.Opaque = !material.Flags.IsTranslucent;
			renderer.Additive = material.GetFeature( "F_ADDITIVE_BLEND" ) != 0;
			renderer.CastShadows = Source.Shadows;
			renderer.Lighting = Source.Lit;
			renderer.Texturing = new() { Material = material, Scale = 1f };
			renderer.Enabled = Source.Enabled;
		}
	}
}

namespace UnityMount;

/// <summary>Selects the authored mesh detail of an imported object for the main camera.</summary>
[Title( "Mounted LOD Group" )]
public sealed class UnityLodGroup : Component, Component.DontExecuteOnServer
{
	public sealed record Level( float ScreenHeight, ModelRenderer[] Renderers );

	[Property, Hide] public Vector3 ReferencePoint { get; set; }
	[Property, Hide] public float Size { get; set; }
	[Property, Hide] public Level[] Levels { get; set; } = [];
	public int CurrentLevel { get; private set; } = -1;

	Dictionary<ModelRenderer, bool> _authored;
	int _selection = int.MinValue;

	protected override void OnUpdate()
	{
		if ( Levels.Length == 0 ) return;
		_authored ??= Levels.SelectMany( level => level.Renderers ).Where( renderer => renderer.IsValid() )
			.Distinct().ToDictionary( renderer => renderer, renderer => renderer.Enabled );

		var level = Scene.Camera is { } camera ? SelectLevel( ScreenHeight( camera ) ) : 0;
		if ( level == _selection ) return;
		_selection = CurrentLevel = level;
		var selected = level < 0 ? [] : Levels[level].Renderers;
		foreach ( var (renderer, enabled) in _authored )
			if ( renderer.IsValid() ) renderer.Enabled = enabled && selected.Contains( renderer );
	}

	protected override void OnDisabled()
	{
		if ( _authored is not null )
			foreach ( var (renderer, enabled) in _authored )
				if ( renderer.IsValid() ) renderer.Enabled = enabled;
		_authored = null;
		_selection = int.MinValue;
		CurrentLevel = -1;
	}

	int SelectLevel( float height )
	{
		for ( var i = 0; i < Levels.Length; i++ )
			if ( height >= Levels[i].ScreenHeight ) return i;
		return -1;
	}

	float ScreenHeight( CameraComponent camera )
	{
		var scale = WorldScale;
		var size = Size * MathF.Max( MathF.Abs( scale.x ), MathF.Max( MathF.Abs( scale.y ), MathF.Abs( scale.z ) ) );
		if ( camera.Orthographic )
			return camera.OrthographicHeight > 0 ? size / camera.OrthographicHeight : float.PositiveInfinity;

		var tangent = MathF.Tan( camera.FieldOfView * (MathF.PI / 360) );
		if ( camera.FovAxis == CameraComponent.Axis.Horizontal && camera.ScreenRect.Width > 0 )
			tangent *= camera.ScreenRect.Height / camera.ScreenRect.Width;
		var distance = camera.WorldPosition.Distance( WorldTransform.PointToWorld( ReferencePoint ) );
		return tangent > 0 && distance > 0 ? size / (2 * distance * tangent) : float.PositiveInfinity;
	}
}

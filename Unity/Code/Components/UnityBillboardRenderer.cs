namespace UnityMount;

[Title( "Mounted Billboard" )]
public sealed class UnityBillboardRenderer : Component, Component.DontExecuteOnServer, Component.ExecuteInEditor
{
	internal const string AtlasGroup = "atlas";

	[Property, Hide] public ModelRenderer Renderer { get; set; }
	[Property, Hide] public int ImageCount { get; set; }

	protected override void OnPreRender()
	{
		if ( ImageCount < 1 || !Renderer.IsValid() || !Renderer.Active || Scene.Camera is not { } camera ) return;

		var view = camera.Orthographic ? -camera.WorldRotation.Forward : camera.WorldPosition - WorldPosition;
		var direction = view.WithZ( 0 ).Normal;
		if ( direction.IsNearlyZero() ) return;

		WorldRotation = Rotation.LookAt( direction, Vector3.Up );

		var forward = GameObject.Parent.WorldRotation.Forward.WithZ( 0 ).Normal;
		if ( forward.IsNearlyZero() ) forward = Vector3.Forward;
		var local = Rotation.LookAt( forward, Vector3.Up ).Inverse * direction;
		// Atlas image zero faces Unity's +X, which maps to Source's right axis.
		var angle = MathF.Atan2( local.x, -local.y );
		if ( angle < 0f ) angle += 2f * MathF.PI;
		var image = (int)MathF.Floor( angle * (ImageCount / (2f * MathF.PI)) + 0.5f ) % ImageCount;
		Renderer.SetBodyGroup( AtlasGroup, image );
	}
}
